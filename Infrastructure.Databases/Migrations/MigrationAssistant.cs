using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Rtl.News.RtlPoc.Application.Shared;
using System.Collections.Frozen;
using UniqueKey = Rtl.News.RtlPoc.Domain.Shared.UniqueKey;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.Migrations;

/// <summary>
/// <para>
/// Helps perform migrations without concurrency conflicts.
/// </para>
/// <para>
/// Although <see cref="MigrateAsync"/> can be invoked directly, that method is normally invoked by registering this type as <see cref="IHostedService"/> and starting the host.
/// </para>
/// </summary>
internal sealed class MigrationAssistant(
	ILogger<MigrationAssistant> logger,
	IResilienceStrategy resilienceStrategy,
	DatabaseClient databaseClient,
	IMomentaryLockFactory momentaryLockFactory,
	MigrationListProvider migrationListProvider)
	: IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		return this.MigrateAsync(cancellationToken);
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask; // Nothing to do
	}

	/// <summary>
	/// Performs all database migrations in a concurrency-safe way.
	/// </summary>
	public async Task MigrateAsync(CancellationToken cancellationToken)
	{
		logger.LogInformation("Migrating");

		var migrations = migrationListProvider.GetMigrations();
		var targetMigrationCount = migrations.Count;

		while (!cancellationToken.IsCancellationRequested)
		{
			// If no migrations, done
			var migrationCount = await this.GetMigrationCountAsync(cancellationToken);
			if (migrationCount == targetMigrationCount)
				break;

			// Obtain a short-lived global lock on migration 0, so that each migration is started by only one replica
			var migrationForLocking = new Migration(count: 0, description: "Initial migration");
			var migrationKey = UniqueKey.Create(() => migrationForLocking.Count, migrationForLocking.Count);
			await using var migrationLock = await momentaryLockFactory.WaitAsync(migrationKey, cancellationToken);

			// Double check within lock
			migrationCount = await this.GetMigrationCountAsync(cancellationToken);
			if (migrationCount == targetMigrationCount)
				break;

			// Migrate
			await this.StartMigrationAsync(migrations, currentMigrationCount: migrationCount, cancellationToken);
		}

		logger.LogInformation("Migrated");
	}

	private async Task<ushort> GetMigrationCountAsync(CancellationToken cancellationToken)
	{
		var queryable = databaseClient.Container.GetItemLinqQueryable<Migration>(requestOptions: new QueryRequestOptions()
		{
			PartitionKey = new PartitionKey(Migration.DefaultPartitionKey),
			ConsistencyLevel = ConsistencyLevel.BoundedStaleness, // Must be up-to-date
			EnableScanInQuery = false,
			MaxItemCount = 1,
		});

		var query = queryable.OrderByDescending(x => x.Id);

		return await resilienceStrategy.ExecuteAsync(
			async cancellationToken =>
			{
				using var iterator = query.ToFeedIterator();
				var batch = await iterator.ReadNextAsync(cancellationToken);
				var result = batch.SingleOrDefault();
				return result?.Count ?? 0;
			},
			cancellationToken);
	}

	private async Task StartMigrationAsync(FrozenDictionary<string, Action<ContainerProperties>> migrations, ushort currentMigrationCount, CancellationToken cancellationToken)
	{
		var (description, mutation) = migrations.ElementAt(currentMigrationCount);
		var migration = new Migration(count: ++currentMigrationCount, description: description);

		logger.LogInformation("Migrating to #{MigrationCount}: {Description}", migration.Count, migration.Description);

		ContainerProperties containerProperties = await databaseClient.Container.ReadContainerAsync(cancellationToken: cancellationToken);

		// Apply the mutation to the properties
		mutation(containerProperties);

		// Start the operation of altering the container, which CosmosDB itself will continue in the background
		await resilienceStrategy.ExecuteAsync(
			cancellationToken => databaseClient.Container.ReplaceContainerAsync(containerProperties, cancellationToken: cancellationToken),
			cancellationToken);

		// Store the fact that the migration was initiated
		await resilienceStrategy.ExecuteAsync(
			cancellationToken => databaseClient.Container.CreateItemAsync(migration, new PartitionKey(migration.PartitionKey), cancellationToken: cancellationToken),
			CancellationToken.None); // Without cancellation

		logger.LogInformation("Migrated to #{MigrationCount}: {Description}", migration.Count, migration.Description);
	}

	internal sealed class Migration : IPocEntity
	{
		public override string ToString() => $"{{{nameof(Migration)} #{this.Count}: {this.Description}}}";

		public static DataPartitionKey DefaultPartitionKey { get; } = DataPartitionKey.CreateForArbitraryString("Migrations");

		[JsonProperty("id")]
		public string Id => $"Migration{this.Count:00000}";

		public string GetId() => this.Id;

		[JsonProperty("part")]
		public DataPartitionKey PartitionKey => DefaultPartitionKey;

		[JsonProperty("Migration_Cnt")]
		public ushort Count { get; private init; }

		[JsonProperty("Migration_Dscr")]
		public string? Description { get; private init; } = "";
		public string? ETag { get; set; }

		[JsonConstructor]
		public Migration()
		{
		}

		public Migration(ushort count, string? description)
		{
			this.Count = count;
			this.Description = description;
		}
	}
}
