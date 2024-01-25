using System.Reflection;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Rtl.News.RtlPoc.Domain;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.Shared;

public sealed class DatabaseClient : IHostedService, IDisposable
{
    public CosmosClient CosmosClient { get; }
    public Database Database { get; private set; } = null!;
    public Container Container { get; private set; } = null!;

    private readonly IConfiguration _configuration;

    public DatabaseClient(IConfiguration configuration)
    {
        _configuration = configuration;

        var boundedContextName = BoundedContext.Name.Split('.').Last();
        var assemblyNameSuffix = Assembly.GetEntryAssembly()!.GetName().Name!;
        assemblyNameSuffix = assemblyNameSuffix.Contains(boundedContextName)
            ? '_' + assemblyNameSuffix.Split('.').Last()
            : null;

        var cosmosClientOptions = new CosmosClientOptions()
        {
            ApplicationName = $"{boundedContextName}{assemblyNameSuffix}",
            ConnectionMode = ConnectionMode.Direct, // TODO: Are ports 10_000 to 20_000 accessible? Otherwise, use Gateway mode.
            ConsistencyLevel = ConsistencyLevel.Session, // Can use session reads by default while having the database allow up to BoundedStaleness, for maximum flexibility (writes are the same for these)
            MaxRetryAttemptsOnRateLimitedRequests = 9,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30),
            EnableContentResponseOnWrite = false,
            Serializer = new CosmosNewtonsoftSerializerHonoringDefaults(),
        };

        CosmosClient = new CosmosClient(
            connectionString: _configuration.GetConnectionString("CoreDatabase"),
            cosmosClientOptions);
    }

    public void Dispose()
    {
        CosmosClient.Dispose();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var databaseName = _configuration.GetConnectionString("CoreDatabaseName");
        var throughput = ThroughputProperties.CreateAutoscaleThroughput(1000);

        var containerProperties = new ContainerProperties(id: "Core", partitionKeyPath: "/part")
        {
            DefaultTimeToLive = -1, // Enable TTL for the container via '/ttl' path, but do not expire by default
            IndexingPolicy = new IndexingPolicy()
            {
                Automatic = true,
                IndexingMode = IndexingMode.Consistent,
                IncludedPaths = { new IncludedPath() { Path = "/*" } }, // See MigrationList for deviations
            },
        };

        Database = await CosmosClient.CreateDatabaseIfNotExistsAsync(
            databaseName,
            throughput,
            cancellationToken: cancellationToken);

        Container = await Database.CreateContainerIfNotExistsAsync(
            containerProperties,
            throughput: null, // Share in database's throughput
            cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
