using System.Net;
using Microsoft.Azure.Cosmos;
using Polly;
using Polly.Retry;
using Prometheus;
using Rtl.News.RtlPoc.Application.Shared;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.Shared;

/// <summary>
/// <para>
/// An <see cref="IResilienceStrategy"/> for CosmosDB.
/// </para>
/// <para>
/// Optimistic concurrency conflicts lead to retries with fuzzy, gradual backoff.
/// </para>
/// </summary>
public sealed class CosmosResilienceStrategy : IResilienceStrategy
{
    internal static readonly Histogram ConcurrencyConflictRetryHistogram = Metrics.CreateHistogram(
        "ConcurrencyConflictRetries",
        "The number of occurrences per Nth retry attempt due to concurrency conflicts (e.g. 2 retries will tick as both 1 and 2)",
        new HistogramConfiguration()
        {
            Buckets = Histogram.LinearBuckets(start: 1, width: 1, count: 5),
        });

    private readonly ResiliencePipeline _resiliencePipeline;

    public CosmosResilienceStrategy()
    {
        _resiliencePipeline = new ResiliencePipelineBuilder()

            // The SDK already handles retries for rate limiting

            // The SDK already handles retries for transient failures (and correctly NOT for writes, which are not guaranteed to be idempotent)
            // https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/conceptual-resilient-sdk-applications#timeouts-and-connectivity-related-failures-http-408503

            // Retry on optimistic concurrency conflict, i.e. CosmosException with HttpStatusCode.PreconditionFailed
            .AddRetry(new RetryStrategyOptions()
            {
                ShouldHandle = new PredicateBuilder().Handle<CosmosException>(e => e.StatusCode == HttpStatusCode.PreconditionFailed),
                MaxRetryAttempts = 5,
                UseJitter = true, // Fuzziness reduces continued contention
                DelayGenerator = args => ValueTask.FromResult(args.AttemptNumber switch
                {
                    0 => (TimeSpan?)TimeSpan.Zero,
                    1 => TimeSpan.FromMilliseconds(30),
                    _ => TimeSpan.FromSeconds(1),
                }),
                OnRetry = args => { ConcurrencyConflictRetryHistogram.Observe(1 + args.AttemptNumber); return ValueTask.CompletedTask; },
            })

            .Build();
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        await _resiliencePipeline.ExecuteAsync(
            static (operation, cancellationToken) => new ValueTask(task: operation(cancellationToken)),
            operation,
            cancellationToken);
    }

    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        return await _resiliencePipeline.ExecuteAsync(
            static (operation, cancellationToken) => new ValueTask<TResult>(task: operation(cancellationToken)),
            operation,
            cancellationToken);
    }
}
