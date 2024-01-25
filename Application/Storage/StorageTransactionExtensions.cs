namespace Rtl.News.RtlPoc.Application.Storage;

/// <summary>
/// Provides extension methods related to <see cref="StorageTransaction"/> for the purpose of chaining method calls for transactional operations.
/// </summary>
public static class StorageTransactionExtensions
{
    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.AddAsync(IPocEntity, StorageTransaction, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// Adds the given <paramref name="entity"/> that does not yet exist in storage.
    /// </para>
    /// </summary>
    public static ValueTask<StorageTransaction> AddAsync(this ValueTask<StorageTransaction> transactionTask,
        IPocEntity entity)
    {
        return transactionTask.IsCompletedSuccessfully
            ? AddAsync(transactionTask.Result, entity)
            : ProcessAsync(transactionTask, entity);

        static async ValueTask<StorageTransaction> ProcessAsync(ValueTask<StorageTransaction> transactionTask, IPocEntity entity)
        {
            var transaction = await transactionTask;
            return await AddAsync(transaction, entity);
        }
    }
    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.AddAsync(IPocEntity, StorageTransaction, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// Adds the given <paramref name="entity"/> that does not yet exist in storage.
    /// </para>
    /// </summary>
    public static ValueTask<StorageTransaction> AddAsync(this StorageTransaction transaction,
        IPocEntity entity)
    {
        return transaction.Repository.AddAsync(entity, transaction, transaction.CancellationToken);
    }

    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.AddRangeAsync(IPocEntity, StorageTransaction, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// Adds the given <paramref name="entities"/> that do not yet exist in storage.
    /// </para>
    /// </summary>
    public static ValueTask<StorageTransaction> AddRangeAsync(this ValueTask<StorageTransaction> transactionTask,
        IEnumerable<IPocEntity> entities)
    {
        return transactionTask.IsCompletedSuccessfully
            ? AddRangeAsync(transactionTask.Result, entities)
            : ProcessAsync(transactionTask, entities);

        static async ValueTask<StorageTransaction> ProcessAsync(ValueTask<StorageTransaction> transactionTask, IEnumerable<IPocEntity> entities)
        {
            var transaction = await transactionTask;
            return await AddRangeAsync(transaction, entities);
        }
    }
    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.AddRangeAsync(IPocEntity, StorageTransaction, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// Adds the given <paramref name="entities"/> that do not yet exist in storage.
    /// </para>
    /// </summary>
    public static ValueTask<StorageTransaction> AddRangeAsync(this StorageTransaction transaction,
        IEnumerable<IPocEntity> entities)
    {
        return transaction.Repository.AddRangeAsync(entities, transaction, transaction.CancellationToken);
    }

    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.UpdateAsync(IPocEntity, StorageTransaction, CancellationToken, ModificationOptions?)"/>.
    /// </para>
    /// <para>
    /// Updates the given <paramref name="entity"/> that already exists in storage to match the given state.
    /// </para>
    /// </summary>
    public static ValueTask<StorageTransaction> UpdateAsync(this ValueTask<StorageTransaction> transactionTask,
        IPocEntity entity, ModificationOptions? options = null)
    {
        return transactionTask.IsCompletedSuccessfully
            ? UpdateAsync(transactionTask.Result, entity, options)
            : ProcessAsync(transactionTask, entity, options);

        static async ValueTask<StorageTransaction> ProcessAsync(ValueTask<StorageTransaction> transactionTask, IPocEntity entity, ModificationOptions? options)
        {
            var transaction = await transactionTask;
            return await UpdateAsync(transaction, entity, options);
        }
    }
    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.UpdateAsync(IPocEntity, StorageTransaction, CancellationToken, ModificationOptions?)"/>.
    /// </para>
    /// <para>
    /// Updates the given <paramref name="entity"/> that already exists in storage to match the given state.
    /// </para>
    /// </summary>
    public static ValueTask<StorageTransaction> UpdateAsync(this StorageTransaction transaction,
        IPocEntity entity, ModificationOptions? options = null)
    {
        return transaction.Repository.UpdateAsync(entity, transaction, transaction.CancellationToken, options);
    }

    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.DeleteAsync(IPocEntity, StorageTransaction, CancellationToken, ModificationOptions?)"/>.
    /// </para>
    /// <para>
    /// Deletes the given <paramref name="entity"/> from storage if it exists there.
    /// </para>
    /// </summary>
    public static ValueTask<StorageTransaction> DeleteAsync(this ValueTask<StorageTransaction> transactionTask,
        IPocEntity entity, ModificationOptions? options = null)
    {
        return transactionTask.IsCompletedSuccessfully
            ? DeleteAsync(transactionTask.Result, entity, options)
            : ProcessAsync(transactionTask, entity, options);

        static async ValueTask<StorageTransaction> ProcessAsync(ValueTask<StorageTransaction> transactionTask, IPocEntity entity, ModificationOptions? options)
        {
            var transaction = await transactionTask;
            return await DeleteAsync(transaction, entity, options);
        }
    }
    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.DeleteAsync(IPocEntity, StorageTransaction, CancellationToken, ModificationOptions?)"/>.
    /// </para>
    /// <para>
    /// Deletes the given <paramref name="entity"/> from storage if it exists there.
    /// </para>
    /// </summary>
    public static ValueTask<StorageTransaction> DeleteAsync(this StorageTransaction transaction,
        IPocEntity entity, ModificationOptions? options = null)
    {
        return transaction.Repository.DeleteAsync(entity, transaction, transaction.CancellationToken, options);
    }

    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.DeleteAsync(String, StorageTransaction, CancellationToken, ModificationOptions?)"/>.
    /// </para>
    /// <para>
    /// Deletes the given <paramref name="entity"/> from storage if it exists there.
    /// </para>
    /// <para>
    /// Because no concurrency protection is possible without an entity, this method requires <see cref="ModificationOptions.IgnoresConcurrencyProtection"/> to be passed as <see langword="true"/>, as confirmation.
    /// </para>
    /// </summary>
    public static ValueTask<StorageTransaction> DeleteAsync(this ValueTask<StorageTransaction> transactionTask,
        string id, ModificationOptions? options = null)
    {
        return transactionTask.IsCompletedSuccessfully
            ? DeleteAsync(transactionTask.Result, id, options)
            : ProcessAsync(transactionTask, id, options);

        static async ValueTask<StorageTransaction> ProcessAsync(ValueTask<StorageTransaction> transactionTask, string id, ModificationOptions? options)
        {
            var transaction = await transactionTask;
            return await DeleteAsync(transaction, id, options);
        }
    }
    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.DeleteAsync(String, StorageTransaction, CancellationToken, ModificationOptions?)"/>.
    /// </para>
    /// <para>
    /// Deletes the given <paramref name="entity"/> from storage if it exists there.
    /// </para>
    /// <para>
    /// Because no concurrency protection is possible without an entity, this method requires <see cref="ModificationOptions.IgnoresConcurrencyProtection"/> to be passed as <see langword="true"/>, as confirmation.
    /// </para>
    /// </summary>
    public static ValueTask<StorageTransaction> DeleteAsync(this StorageTransaction transaction,
        string id, ModificationOptions? options = null)
    {
        return transaction.Repository.DeleteAsync(id, transaction, transaction.CancellationToken, options);
    }

    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.CommitAsync(StorageTransaction, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// Commits the given <see cref="StorageTransaction"/> to storage.
    /// </para>
    /// </summary>
    public static Task CommitAsync(this ValueTask<StorageTransaction> transactionTask)
    {
        return transactionTask.IsCompletedSuccessfully && transactionTask.Result is StorageTransaction transaction
            ? transaction.Repository.CommitAsync(transaction, transaction.CancellationToken)
            : ProcessAsync(transactionTask);

        static async Task ProcessAsync(ValueTask<StorageTransaction> transactionTask)
        {
            var transaction = await transactionTask;
            await transaction.Repository.CommitAsync(transaction, transaction.CancellationToken);
        }
    }

    /// <summary>
    /// <para>
    /// Awaits its turn in the chain and then delegates to <see cref="IRepository.RollBackAsync(StorageTransaction, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// Rolls back any mutations made for the given <see cref="StorageTransaction"/> that was not yet committed.
    /// </para>
    /// </summary>
    public static Task RollBackAsync(this ValueTask<StorageTransaction> transactionTask)
    {
        return transactionTask.IsCompletedSuccessfully && transactionTask.Result is StorageTransaction transaction
            ? transaction.Repository.RollBackAsync(transaction, transaction.CancellationToken)
            : ProcessAsync(transactionTask);

        static async Task ProcessAsync(ValueTask<StorageTransaction> transactionTask)
        {
            var transaction = await transactionTask;
            await transaction.Repository.RollBackAsync(transaction, transaction.CancellationToken);
        }
    }
}
