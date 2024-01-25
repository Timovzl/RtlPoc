using Rtl.News.RtlPoc.Domain;

namespace Rtl.News.RtlPoc.Application;

// TODO: Remove when implementing the first real use case

public sealed class ExampleUseCase(
    IRepository repository,
    IPromiseFulfiller promiseFulfiller,
    IMomentaryLockFactory momentaryLockFactory)
    : UseCase
{
    public override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var idGeneratorScope = IdGenerator.CreateIdGeneratorScopeForSinglePartition();

        var jan = new ExampleEntity("Jan");
        var piet = new ExampleEntity("Piet");

        // #TODO: Consider favored approach
        var promiseToRemovePiet = Promise.Create((ExampleUseCase useCase) => useCase.RemoveExampleEntity, data: piet.Id);
        var promiseToRemoveJan = Promise.CreateForEntity(jan, (ExampleUseCase useCase) => useCase.RemoveExampleEntity, data: jan.Id);

        // Already exist (hot path)
        if (await repository.ExistsAsync<ExampleEntity>(
            query => query.Where(x => new[] { jan.Name, piet.Name }.Contains(x.Name)),
            cancellationToken))
        {
            return;
        }

        // Unique key claim
        await using var nameLock = await momentaryLockFactory.WaitRangeAsync(
        [
            UniqueKey.Create(() => jan.Name, jan.Name),
            UniqueKey.Create(() => piet.Name, piet.Name),
        ], cancellationToken);

        // Already exist (race condition)
        if (await repository.ExistsAsync<ExampleEntity>(
            query => query.Where(x => new[] { jan.Name, piet.Name }.Contains(x.Name)),
            cancellationToken,
            new MultiReadOptions() { FullyConsistent = true }))
        {
            return;
        }

        await using var transaction = await repository.CreateTransactionAsync(jan.PartitionKey, cancellationToken);
        await transaction
            .AddAsync(jan)
            .AddAsync(piet)
            .AddAsync(promiseToRemovePiet)
            .AddAsync(promiseToRemoveJan)
            .CommitAsync();

        promiseToRemoveJan.SuppressImmediateFulfillment();
        await promiseFulfiller.TryFulfillAsync(promiseToRemovePiet, cancellationToken);
    }

    [IdempotentPromiseFulfiller("Example_RemoveExampleEntity")]
    private async Task RemoveExampleEntity(Promise promise, CancellationToken cancellationToken)
    {
        var id = promise.Data;

        // #TODO: Consider alternatives to '(DataPartitionKey)id'

        // Idempotency is automatic for delete
        await using var transaction = await repository.CreateTransactionAsync((DataPartitionKey)id, cancellationToken);
        await transaction
            .DeleteAsync(id, new ModificationOptions() { IgnoresConcurrencyProtection = true }) // Do not care about updates in the meantime
            .CommitAsync();
    }
}
