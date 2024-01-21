using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Rtl.News.RtlPoc.Infrastructure.Databases.Migrations;
using Rtl.News.RtlPoc.Infrastructure.Databases.Shared;
using Shouldly;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.IntegrationTests;

public sealed class DatabaseInfrastructureRegistrationExtensionsTests
{
	/// <summary>
	/// The <see cref="MigrationAssistant"/> is to be added separately, deliberately.
	/// </summary>
	[Fact]
	public void AddDatabaseInfrastructureLayer_Regularly_ShouldNotAddMigrationAssistant()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDatabaseInfrastructureLayer(new ConfigurationBuilder().Build());

		// Assert
		services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(MigrationAssistant));
	}

	/// <summary>
	/// The <see cref="CosmosClient"/> <em>really</em> should be used as a singleton.
	/// </summary>
	[Fact]
	public void AddDatabaseInfrastructureLayer_Regularly_ShouldAddSingletonDatabaseClient()
	{
		// Arrange

		var services = new ServiceCollection();
		var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
		config["ConnectionStrings:CoreDatabase"] = "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6;";
		config["ConnectionStrings:CoreDatabaseName"] = "Test";
		services.AddSingleton<IConfiguration>(config);

		// Act

		services.AddDatabaseInfrastructureLayer(config);

		// Assert

		using var serviceProvider = services.BuildServiceProvider();

		var one = serviceProvider.GetService<DatabaseClient>();
		var two = serviceProvider.GetService<DatabaseClient>();

		using var scope = serviceProvider.CreateScope();
		var three = scope.ServiceProvider.GetService<DatabaseClient>();

		two.ShouldBeSameAs(one);
		three.ShouldBeSameAs(one);
	}
}
