using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Rtl.News.RtlPoc.Application.Storage;
using Rtl.News.RtlPoc.Infrastructure.Databases.Migrations;
using Rtl.News.RtlPoc.Infrastructure.Databases.Shared;
using System.Collections.Frozen;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.IntegrationTests.Migrations;

public class MigrationAssistantTests : IntegrationTestBase
{
	[Fact]
	public async Task MigrateAsync_Concurrently_ShouldMakeAndStoreExpectedChangesIdempotently()
	{
		// Arrange

		// Avoid the squashed migrations that our tests usually employ, thus reverting to regular migrations
		this.ConfigureServices(services => services.Remove(services.Last(descriptor => descriptor.ServiceType == typeof(MigrationListProvider))));

		// Prevent migrations during StartAsync(), so that we can test them separately
		var allowMigrations = false;
		this.ConfigureServices(services => services.Decorate<MigrationListProvider>(provider =>
		{
			var suppressableMigrationListProvider = Substitute.For<MigrationListProvider>();
			suppressableMigrationListProvider.GetMigrations().Returns(_ => allowMigrations
				? provider.GetMigrations()
				: FrozenDictionary<string, Action<ContainerProperties>>.Empty);
			return suppressableMigrationListProvider;
		}));

		var logger = Substitute.For<ILogger>();
		this.ConfigureServices(services => services.AddSingleton<ILogger<MigrationAssistant>>(new LoggerWrapperForInternalType<MigrationAssistant>(logger)));

		var instance = this.Host.Services.GetRequiredService<MigrationAssistant>();

		var repository = this.Host.Services.GetRequiredService<IRepository>();
		var migrations = await repository.ListAsync<MigrationAssistant.Migration>(query => query.Where(x => x.Count >= 0), CancellationToken.None, new MultiReadOptions() { FullyConsistent = true });

		migrations.Count.ShouldBe(0); // No migrations should have been applied yet
		logger.ClearReceivedCalls();

		allowMigrations = true;

		// Act

		// Concurrently ask for migrations
		var tasks = Enumerable.Range(0, 4)
			.Select(_ => instance.MigrateAsync(CancellationToken.None))
			.ToList();
		await Task.WhenAll(tasks);

		// Assert

		var expectedMigrations = new MigrationListProvider().GetMigrations();

		migrations = await repository.ListAsync<MigrationAssistant.Migration>(query => query.Where(x => x.Count >= 0), CancellationToken.None, new MultiReadOptions() { FullyConsistent = true });

		migrations.Count.ShouldBe(expectedMigrations.Count);

		// Data set should claim to know all expected migrations
		foreach (var (migration, expectedMigration) in migrations.Zip(expectedMigrations))
			migration.Description.ShouldBe(expectedMigration.Key);

		// Verify changes applied by various migrations
		var container = this.Host.Services.GetRequiredService<DatabaseClient>().Container;
		ContainerProperties containerProperties = await container.ReadContainerAsync();
		containerProperties.IndexingPolicy.IncludedPaths.ShouldHaveSingleItem().Path.ShouldBe("/*");
		containerProperties.IndexingPolicy.ExcludedPaths.ShouldContain(excludedPath => excludedPath.Path == "/Promise_Dta/?");

		// Each individual migration should have been applied and logged exactly once
		for (var i = 1; i < 1 + expectedMigrations.Count; i++)
		{
			logger.Received(1).Log(message => message.StartsWith($"Migrating to #{i}"));
			logger.Received(1).Log(message => message.StartsWith($"Migrated to #{i}"));
		}

		// The overall process of performing migrations should have been logged once for each concurrent execution
		logger.Received(4).Log(message => message.Equals("Migrating"));
		logger.Received(4).Log(message => message.Equals("Migrated"));
	}
}
