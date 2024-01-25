using System.Runtime.CompilerServices;
using Microsoft.Extensions.Hosting;

namespace Rtl.News.RtlPoc.Application.Promises;

/// <summary>
/// A background service that eventually discovers promises that were reneged on, such as due to failures, and continues to make attempts to fulfill them.
/// </summary>
public interface IPromiseSalvager : IHostedService
{
}

public abstract class PromiseSalvager(
    ILogger<PromiseSalvager> logger,
    IResilienceStrategy resilienceStrategy,
    IPromiseFulfiller promiseFulfiller)
    : BackgroundService, IPromiseSalvager
{
    private static readonly TimeSpan AverageDelay = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Periodically attempts to fulfill promises that have not received attention recently.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var averageDelaySeconds = (int)Math.Ceiling(AverageDelay.TotalSeconds);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var delayInSeconds = averageDelaySeconds +
                    Random.Shared.Next(minValue: averageDelaySeconds / -4, maxValue: averageDelaySeconds / 4); // +/- a quarter

                var delayTask = Task.Delay(TimeSpan.FromSeconds(delayInSeconds), stoppingToken);
                var workTask = TryFulfillDuePromisesAsync(stoppingToken);

                await Task.WhenAll(delayTask, workTask);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Terminate smoothly
        }
    }

    /// <summary>
    /// <para>
    /// Performs a single iteration of attempting to fulfill unfulfilled promises that are due, i.e. have not been attempted recently enough.
    /// </para>
    /// <para>
    /// Exceptions are caught and logged.
    /// </para>
    /// </summary>
    internal async Task TryFulfillDuePromisesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var promise in EnumerateDuePromisesAsync(cancellationToken))
                if (await TryClaimAndDeferPromiseAsync(promise, cancellationToken))
                    await promiseFulfiller.TryFulfillAsync(promise, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Will be attempted again soon
        }
        catch (Exception e)
        {
            logger.LogError(e, "Background fulfillment of neglected promises encountered an error: {Exception}: {Message}", e.GetType().Name, e.Message);
        }
    }

    /// <summary>
    /// Enumerates unfulfilled promises that are due, i.e. have not been attempted recently enough.
    /// </summary>
    private async IAsyncEnumerable<Promise> EnumerateDuePromisesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Using separate small batches reduces race conditions with competing processes
        // Finally, to avoid endlessly working on single items trickling in, continue only as long as a full batch was received
        const ushort BatchSize = 10;

        var expectsMoreData = true;
        while (expectsMoreData && !cancellationToken.IsCancellationRequested)
        {
            var batch = await resilienceStrategy.ExecuteAsync(cancellationToken => GetNeglectedPromiseBatchAsync(BatchSize, cancellationToken), cancellationToken);

            foreach (var promise in batch)
                yield return promise;

            expectsMoreData = batch.Count >= BatchSize;
        }
    }

    /// <summary>
    /// Attempts to delay further evaluation of the given <paramref name="promise"/>, thereby claiming it for the duration.
    /// A negative result indicates that the promise was snatched by a competing process.
    /// </summary>
    private Task<bool> TryClaimAndDeferPromiseAsync(Promise promise, CancellationToken cancellationToken)
    {
        promise.ClaimForAttempt();

        return resilienceStrategy.ExecuteAsync(cancellationToken => TryUpdatePromiseAsync(promise, cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Makes a single attempt to retrieve the next batch of <paramref name="batchSize"/> promises.
    /// </summary>
    protected abstract Task<IReadOnlyList<Promise>> GetNeglectedPromiseBatchAsync(ushort batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Makes a single attempt to update the given promise, returning true on success, or returning false and making no changes if the stored data has been modified in the meantime.
    /// </summary>
    protected abstract Task<bool> TryUpdatePromiseAsync(Promise promise, CancellationToken cancellationToken);
}
