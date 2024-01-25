using Microsoft.Azure.Cosmos;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.Shared;

internal static class StorageTransactionExtensions
{
    /// <summary>
    /// <para>
    /// Allows exactly one operation to be added to a <see cref="CosmosTransaction"/>.
    /// </para>
    /// <para>
    /// Do <em>not</em> add zero or multiple operations when calling this method.
    /// The entities are tracked in-sync with the operations (such as for updating ETags after committing), and CosmosDB transactions currently provide no way to check the actual accumulated number of operations.
    /// </para>
    /// </summary>
    /// <param name="entity">Null for deletes, required otherwise.</param>
    public static void AddOperationToBatch(this StorageTransaction transaction, IPocEntity? entity, Action<TransactionalBatch> operation)
    {
        var cosmosTransaction = (CosmosTransaction)transaction;
        cosmosTransaction.SetEntityForNextOperation(entity);
        operation(cosmosTransaction.Batch);
    }
}
