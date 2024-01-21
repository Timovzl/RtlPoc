using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Rtl.News.RtlPoc.Application;
using Rtl.News.RtlPoc.Infrastructure.Databases.Migrations;
using Rtl.News.RtlPoc.Infrastructure.Databases.Shared;
using System.Collections.Frozen;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.IntegrationTests;

// #TODO: Use a shared CosmosClient between tests
public abstract class IntegrationTestBase : IDisposable
{
	/// <summary>
	/// The current time zone's offset from UTC during January. Useful for replacements in JSON strings to make assertions on.
	/// </summary>
	protected static string TimeZoneUtcOffsetString { get; } = $"+{TimeZoneInfo.Local.GetUtcOffset(DateTime.UnixEpoch):hh\\:mm}";

	protected string UniqueTestName { get; } = $"Test_{DistributedId128.CreateGuid().ToAlphanumeric()}";
	/// <summary>
	/// A fixed timestamp on January 1 in the future, with a nonzero value for hours, minutes, seconds, milliseconds, and ticks.
	/// The nonzero time components help test edge cases, such as rounding or truncation by the database.
	/// </summary>
	protected static readonly DateTime UtcNow = new DateTime(3000, 01, 01, 01, 01, 01, millisecond: 01, DateTimeKind.Utc).AddTicks(1);
	/// <summary>
	/// A fixed timestamp on January 1 in the future.
	/// </summary>
	protected static readonly DateTime UtcToday = UtcNow.Date;

	protected IHostBuilder HostBuilder { get; set; }

	protected IConfiguration Configuration { get; }

	/// <summary>
	/// <para>
	/// Returns the host, which contains the services.
	/// </para>
	/// <para>
	/// On the first resolution, the host is built and started.
	/// </para>
	/// <para>
	/// If the host is started, it is automatically stopped when the test class is disposed.
	/// </para>
	/// </summary>
	protected IHost Host
	{
		get
		{
			if (this._host is null)
			{
				this._host ??= this.HostBuilder.Build();
				this._host.Start();
			}
			return this._host;
		}
	}
	private IHost? _host;

	protected IntegrationTestBase()
	{
		this.HostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
			.UseDefaultServiceProvider(provider => provider.ValidateOnBuild = provider.ValidateScopes = true); // Be as strict as ASP.NET Core in Development is

		this.Configuration = new ConfigurationBuilder()
			.AddJsonFile("appsettings.json")
			.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json", optional: true)
			.AddEnvironmentVariables()
			.Build();

		this.Configuration["ConnectionStrings:CoreDatabaseName"] = this.UniqueTestName;

		this.ConfigureServices(services => services.AddSingleton(this.Configuration));

		this.ConfigureServices(services => services.AddApplicationLayer(this.Configuration));
		this.ConfigureServices(services => services.AddDatabaseInfrastructureLayer(this.Configuration));

		// Add migrations, but squashed into a single migration for efficiency
		this.ConfigureServices(services => services.AddDatabaseMigrations());
		var defaultMigrationListProvider = new MigrationListProvider();
		var squashedMigrations = new[] { defaultMigrationListProvider.ApplyAllMigrations }.ToFrozenDictionary(_ => "Squashed migration");
		var squashedMigrationListProvider = Substitute.For<MigrationListProvider>();
		squashedMigrationListProvider.GetMigrations().Returns(squashedMigrations);
		this.ConfigureServices(services => services.AddSingleton(squashedMigrationListProvider));
	}

	public virtual void Dispose()
	{
		GC.SuppressFinalize(this);

		try
		{
			this._host?.StopAsync().GetAwaiter().GetResult();
		}
		finally
		{
			if (this._host is not null)
				this.DeleteDatabaseAsync().GetAwaiter().GetResult();

			this._host?.Dispose();
		}
	}

	/// <summary>
	/// Adds an action to be executed as part of what would normally be Startup.ConfigureServices().
	/// </summary>
	protected void ConfigureServices(Action<IServiceCollection> action)
	{
		if (this._host is not null) throw new Exception("No more services can be registered once the host is resolved.");

		this.HostBuilder.ConfigureServices(action ?? throw new ArgumentNullException(nameof(action)));
	}

	/// <summary>
	/// Deletes the current test's database if it exists.
	/// </summary>
	private Task<DatabaseResponse> DeleteDatabaseAsync()
	{
		var databaseClient = this.Host.Services.GetRequiredService<DatabaseClient>();
		return databaseClient.Container.Database.DeleteAsync();
	}
}
