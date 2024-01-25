using System.Collections.Frozen;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Rtl.News.RtlPoc.Application.Storage;
using Rtl.News.RtlPoc.Infrastructure.Databases;
using Rtl.News.RtlPoc.Infrastructure.Databases.Migrations;
using Rtl.News.RtlPoc.Infrastructure.Databases.Shared;

namespace Rtl.News.RtlPoc.Application.IntegrationTests;

// #TODO: Use a shared CosmosClient between tests
public abstract class IntegrationTestBase : IDisposable
{
    /// <summary>
    /// The current time zone's offset from UTC during January. Useful for replacements in JSON strings to make assertions on.
    /// </summary>
    protected static string TimeZoneUtcOffsetString { get; } = $"+{TimeZoneInfo.Local.GetUtcOffset(DateTime.UnixEpoch):hh\\:mm}";
    protected static JsonSerializerOptions JsonSerializerOptions { get; } = new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter() } };

    /// <summary>
    /// A fixed timestamp on January 1 in the future, matching <see cref="FixedTime"/>, but without sub-millisecond components.
    /// The nonzero time components help test edge cases, such as rounding or truncation by the database.
    /// </summary>
    protected static readonly DateTime RoundedFixedTime = new DateTime(3000, 01, 01, 01, 01, 01, millisecond: 01, DateTimeKind.Utc);
    /// <summary>
    /// A fixed timestamp on January 1 in the future, with a nonzero value for hours, minutes, seconds, milliseconds, and ticks.
    /// The nonzero time components help test edge cases, such as rounding or truncation by the database.
    /// </summary>
    protected static readonly DateTime FixedTime = new DateTime(3000, 01, 01, 01, 01, 01, millisecond: 01, DateTimeKind.Utc).AddTicks(1);
    /// <summary>
    /// A fixed date on January 1 in the future, matching the date of <see cref="FixedTime"/>.
    /// </summary>
    protected static readonly DateOnly FixedDate = DateOnly.FromDateTime(FixedTime);

    protected string UniqueTestName { get; } = $"Test_{DistributedId128.CreateGuid().ToAlphanumeric()}";

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
                this._host = this.HostBuilder.Build();
                this._host.Start();
            }
            return this._host;
        }
    }
    private IHost? _host;

    public IRepository Repository => this.Host.Services.GetRequiredService<IRepository>();

    protected IntegrationTestBase()
    {
        this.HostBuilder = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(provider => provider.ValidateOnBuild = provider.ValidateScopes = true) // Be as strict as ASP.NET Core in Development is
            .ConfigureWebHostDefaults(webBuilder => webBuilder
                .Configure(appBuilder => appBuilder
                    .UseRouting()
                    .UseApplicationControllers())
                .UseTestServer(options => options.PreserveExecutionContext = true));

        this.Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        this.Configuration["ConnectionStrings:CoreDatabaseName"] = this.UniqueTestName;

        this.ConfigureServices(services => services.AddSingleton(this.Configuration));

        HashSet<ServiceDescriptor> existingHostedServices = null!;
        this.ConfigureServices(services => existingHostedServices = services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService)).ToHashSet());

        this.ConfigureServices(services => services.AddApplicationLayer(this.Configuration));

        // Remove custom hosted services, to avoid running startup/background logic
        this.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) && !existingHostedServices.Contains(descriptor)).ToList())
                services.Remove(descriptor);
        });

        this.ConfigureServices(services => services.AddDatabaseInfrastructureLayer(this.Configuration));

        // Add migrations, but squashed into a single migration for efficiency
        this.ConfigureServices(services => services.AddDatabaseMigrations());
        var defaultMigrationListProvider = new MigrationListProvider();
        var squashedMigrations = new[] { defaultMigrationListProvider.ApplyAllMigrations }.ToFrozenDictionary(_ => "Squashed migration");
        var squashedMigrationListProvider = Substitute.For<MigrationListProvider>();
        squashedMigrationListProvider.GetMigrations().Returns(squashedMigrations);
        this.ConfigureServices(services => services.AddSingleton(squashedMigrationListProvider));

        this.ConfigureServices(services => services
            .AddApplicationControllers()
            .AddApplicationPart(typeof(Api.Program).Assembly));
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
        if (this._host is not null)
            throw new NotSupportedException("No more services can be registered once the host is resolved.");

        this.HostBuilder.ConfigureServices(action ?? throw new ArgumentNullException(nameof(action)));
    }

    /// <summary>
    /// <para>
    /// Performs a request as if it came from the outside, using the middleware pipeline, for maximum coverage.
    /// </para>
    /// <para>
    /// Throws an <see cref="HttpRequestException"/> if the response has a non-success status code.
    /// </para>
    /// <para>
    /// The request is handled in the current ExecutionContext, with access to the test's ambient contexts, such as ClockScope.
    /// </para>
    /// </summary>
    protected async Task GetApiResponse<TRequest>(HttpMethod verb, string route, TRequest request)
    {
        using var httpClient = this.Host.GetTestClient();

        var requestJson = JsonSerializer.Serialize(request);

        using var requestMessage = new HttpRequestMessage(verb, route)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json"),
        };

        using var response = await httpClient.SendAsync(requestMessage);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            if (String.IsNullOrEmpty(errorMessage)) errorMessage = response.StatusCode.ToString();
            throw new HttpRequestException(errorMessage);
        }
    }

    /// <summary>
    /// <para>
    /// Performs a request as if it came from the outside, using the middleware pipeline, for maximum coverage.
    /// </para>
    /// <para>
    /// Throws an <see cref="HttpRequestException"/> if the response has a non-success status code.
    /// </para>
    /// <para>
    /// The request is handled in the current ExecutionContext, with access to the test's ambient contexts, such as ClockScope.
    /// </para>
    /// </summary>
    protected async Task<TResponse?> GetApiResponse<TRequest, TResponse>(HttpMethod verb, string route, TRequest request)
    {
        using var httpClient = this.Host.GetTestClient();

        using var requestMessage = new HttpRequestMessage(verb, route);

        if (verb == HttpMethod.Get)
        {
            if (request is not null)
            {
                var culture = new CultureInfo(CultureInfo.InvariantCulture.Name);
                culture.DateTimeFormat.ShortDatePattern = "yyyy-MM-ddTHH:mm:ss.fffK";
                culture.DateTimeFormat.LongTimePattern = "";

                var queryParameters = typeof(TRequest).GetProperties()
                    .Select(property => $"{property.Name}={Uri.EscapeDataString(Convert.ToString(property.GetValue(request), culture) ?? "")}");

                requestMessage.RequestUri = new Uri($"{requestMessage.RequestUri}?{String.Join('&', queryParameters)}", UriKind.Relative);
            }
        }
        else
        {
            var requestJson = JsonSerializer.Serialize(request, JsonSerializerOptions);
            requestMessage.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        }

        using var response = await httpClient.SendAsync(requestMessage);

        if (!response.IsSuccessStatusCode)
        {
            var errorMessage = await response.Content.ReadAsStringAsync();
            if (String.IsNullOrEmpty(errorMessage)) errorMessage = response.StatusCode.ToString();
            throw new HttpRequestException(errorMessage);
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<TResponse>(responseJson, JsonSerializerOptions);

        return result;
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
