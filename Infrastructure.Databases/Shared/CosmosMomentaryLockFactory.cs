using System.Diagnostics;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Rtl.News.RtlPoc.Application.Shared;
using UniqueKey = Rtl.News.RtlPoc.Domain.Shared.UniqueKey;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.Shared;

/// <summary>
/// An <see cref="IMomentaryLockFactory"/> that provides very short-lived locks on CosmosDB resources.
/// </summary>
public sealed class CosmosMomentaryLockFactory(
    ILogger<CosmosMomentaryLockFactory> logger,
    IResilienceStrategy resilienceStrategy,
    DatabaseClient databaseClient)
    : IMomentaryLockFactory
{
    private static readonly ResiliencePipeline RetryOnConflictPipeline = new ResiliencePipelineBuilder()
        .AddRetry(new RetryStrategyOptions()
        {
            ShouldHandle = new PredicateBuilder().Handle<CosmosException>(e => e.StatusCode == HttpStatusCode.Conflict),
            MaxRetryAttempts = 10,
            UseJitter = true, // Fuzziness reduces continued contention
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromMilliseconds(30),
        })
        .Build();

    public Task<IMomentaryLock> WaitAsync(UniqueKey uniqueKey, CancellationToken cancellationToken)
    {
        return RetryOnConflictPipeline
            .ExecuteAsync(
                cancellationToken => new ValueTask<IMomentaryLock>(task: AcquireLockAsync(uniqueKey, cancellationToken)),
                cancellationToken)
            .AsTask();
    }

    public async Task<IMomentaryLock> WaitRangeAsync(IEnumerable<UniqueKey> uniqueKeys, CancellationToken cancellationToken)
    {
        // Sort to avoid deadlocks with unrelated processes
        var uniqueKeyList = uniqueKeys.ToList();
        uniqueKeyList.Sort();

        var lockSynchronizer = new LockSynchronizer(requiredLockCount: uniqueKeyList.Count);
        var locks = new List<CosmosMomentaryLock>(capacity: uniqueKeyList.Count);
        var lockHoldingTasks = new List<Task>(capacity: uniqueKeyList.Count);

        foreach (var uniqueKey in uniqueKeyList)
        {
            var momentaryLock = (CosmosMomentaryLock)await WaitAsync(uniqueKey, cancellationToken);
            locks.Add(momentaryLock);

            var lockHoldingTask = HoldLockAsync(uniqueKey, lockSynchronizer, cancellationToken);
            lockHoldingTasks.Add(lockHoldingTask);
        }

        await Task.WhenAll(lockHoldingTasks);

        // Note that there is no need to dispose the individual lock objects, since we take care of releasing in the combined one
        var result = new CosmosMomentaryLock(
            releaseAction: () => Task.WhenAll(locks.Select(momentaryLock => momentaryLock.ReleaseAction())),
            timeToLive: UniqueKey.DefaultTimeToLive / 2,
            disposedWhileExpiredAction: () => locks.ForEach(momentaryLock => momentaryLock.DisposedWhileExpiredAction()));
        return result;
    }

    /// <summary>
    /// <para>
    /// Completes successfully once the <see cref="LockSynchronizer"/> indicates that all locks are acquired.
    /// </para>
    /// <para>
    /// While waiting, prevents the lock from by periodically refreshing it.
    /// </para>
    /// <para>
    /// The lock is only guaranteed to have been successfully acquired if this method completes successfully.
    /// </para>
    /// </summary>
    private async Task HoldLockAsync(UniqueKey uniqueKey, LockSynchronizer lockSynchronizer, CancellationToken cancellationToken)
    {
        do
        {
            // Report that our lock was acquired
            lockSynchronizer.NotifyLockAcquired();

            // If all locks were acquired, we are done
            if (lockSynchronizer.AllLocksAcquired.IsCompletedSuccessfully)
                return;

            try
            {
                // Wait until all locks are acquired
                // But if our lock is halfway along to expiring, then we should update its TTL to avoid the risk of losing it
                await lockSynchronizer.AllLocksAcquired.WaitAsync(timeout: UniqueKey.DefaultTimeToLive / 2, cancellationToken);
            }
            catch (TimeoutException)
            {
                // Report that our lock is no longer definitively acquired
                lockSynchronizer.NotifyLockLost();

                // Update our lock to hold it, so that we can retry from the top
                // But if we cannot do so in time, then we have failed and must throw
                var updateTask = RefreshLockTtlAsync(uniqueKey, cancellationToken);
                await updateTask.WaitAsync(timeout: UniqueKey.DefaultTimeToLive / 2, cancellationToken);
            }
        } while (!lockSynchronizer.AllLocksAcquired.IsCompletedSuccessfully);
    }

    private Task<ItemResponse<UniqueKey>> RefreshLockTtlAsync(UniqueKey uniqueKey, CancellationToken cancellationToken)
    {
        return resilienceStrategy.ExecuteAsync(
            cancellationToken => databaseClient.Container.PatchItemAsync<UniqueKey>(
                uniqueKey.Id,
                new PartitionKey(uniqueKey.PartitionKey),
                cancellationToken: cancellationToken,
                patchOperations:
                [
                    PatchOperation.Set(JsonUtilities.GetPropertyPath(() => uniqueKey.TimeToLiveInSeconds), (ushort)UniqueKey.DefaultTimeToLive.TotalSeconds)
                ],
                requestOptions: new PatchItemRequestOptions()
                {
                    IndexingDirective = IndexingDirective.Exclude, // For performance, avoid indexing of the properties of locks
                }),
            cancellationToken);
    }

    private async Task<IMomentaryLock> AcquireLockAsync(UniqueKey uniqueKey, CancellationToken cancellationToken)
    {
        await resilienceStrategy.ExecuteAsync(
            cancellationToken => databaseClient.Container.CreateItemAsync(uniqueKey, new PartitionKey(uniqueKey.PartitionKey), cancellationToken: cancellationToken, requestOptions: new ItemRequestOptions()
            {
                IndexingDirective = IndexingDirective.Exclude, // For performance, avoid indexing of the properties of locks
            }),
            cancellationToken);

        var result = new CosmosMomentaryLock(
            releaseAction: () => ReleaseLockAsync(uniqueKey, CancellationToken.None),
            timeToLive: TimeSpan.FromSeconds(uniqueKey.TimeToLiveInSeconds),
            disposedWhileExpiredAction: () => logger.LogWarning($"CosmosDB {nameof(IMomentaryLock)} expired before it was disposed, breaking concurrency safety"));

        return result;
    }

    private Task<ItemResponse<UniqueKey>> ReleaseLockAsync(UniqueKey uniqueKey, CancellationToken cancellationToken)
    {
        return resilienceStrategy.ExecuteAsync(
            cancellationToken => databaseClient.Container.DeleteItemAsync<UniqueKey>(uniqueKey.Id, new PartitionKey(uniqueKey.PartitionKey), cancellationToken: cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// A CosmosDB <see cref="IMomentaryLock"/> that release its lock when it is disposed and reports potential early expiration.
    /// </summary>
    /// <param name="disposedWhileExpiredAction">Must not throw.</param>
    private sealed class CosmosMomentaryLock(
        Func<Task> releaseAction,
        TimeSpan timeToLive,
        Action disposedWhileExpiredAction)
        : IMomentaryLock
    {
        internal Func<Task> ReleaseAction => releaseAction;
        internal Action DisposedWhileExpiredAction => disposedWhileExpiredAction;

        private readonly Stopwatch _lifetimeStopwatch = Stopwatch.StartNew();

        public ValueTask DisposeAsync()
        {
            var result = new ValueTask(task: releaseAction());

            try
            {
                if (_lifetimeStopwatch.Elapsed > timeToLive)
                    disposedWhileExpiredAction();
            }
            catch
            {
                // We did our best to clean up
            }

            return result;
        }
    }

    /// <summary>
    /// Synchronizes the acquiring of multiple locks, which may need to be refreshed until all are acquired.
    /// </summary>
    /// <param name="requiredLockCount">The number of locks to be acquired simultaneously.</param>
    private sealed class LockSynchronizer(
        int requiredLockCount)
    {
        private readonly TaskCompletionSource _allLocksAcquired = new TaskCompletionSource();
        public Task AllLocksAcquired => _allLocksAcquired.Task;

        public void NotifyLockAcquired()
        {
            var result = Interlocked.Decrement(ref requiredLockCount);

            // Once all locks are acquired simultaneously, all lock holders are expected to respond to the completed task, and we are done
            if (result == 0)
                _allLocksAcquired.TrySetResult();
        }

        public void NotifyLockLost()
        {
            Interlocked.Increment(ref requiredLockCount);
        }
    }
}
