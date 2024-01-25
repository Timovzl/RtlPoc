using System.Net;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.Shared;

/// <summary>
/// General CosmosDB repository for the core database.
/// </summary>
public sealed partial class CosmosRepository(
    DatabaseClient databaseClient)
    : IRepository
{
    private static readonly ConditionalWeakTable<TransactionalBatch, List<IPocEntity?>> EntitiesPerBatch = [];

    internal Container Container => databaseClient.Container;

    public async Task<T?> GetAsync<T>(string id, DataPartitionKey partitionKey, CancellationToken cancellationToken, ReadOptions? options = null)
        where T : IPocEntity
    {
        options ??= ReadOptions.Default;

        try
        {
            var response = await this.Container.ReadItemAsync<T>(id, new PartitionKey(partitionKey), new ItemRequestOptions()
            {
                ConsistencyLevel = options.FullyConsistent ? ConsistencyLevel.BoundedStaleness : null,
                //SessionToken = "", // TODO Enhancement: Pass session token manually? https://github.com/Azure/azure-cosmos-dotnet-v3/discussions/4237
            }, cancellationToken);

            return response.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }
    }

    public async Task<bool> ExistsAsync<T>(Func<IOrderedQueryable<T>, IQueryable<T>> query, CancellationToken cancellationToken, ReadOptions? options = null)
        where T : class, IPocEntity
    {
        var multiReadOptions = new MultiReadOptions(options)
        {
            PaginationToken = new MutablePaginationToken(pageSize: 1),
        };

        await using var enumerator = this.EnumerateAsync(query, cancellationToken, multiReadOptions).GetAsyncEnumerator(cancellationToken);
        return await enumerator.MoveNextAsync();
    }

    public async Task<T?> LoadAsync<T>(Func<IOrderedQueryable<T>, IQueryable<T>> query, CancellationToken cancellationToken, ReadOptions? options = null)
        where T : class, IPocEntity
    {
        var multiReadOptions = new MultiReadOptions(options)
        {
            PaginationToken = new MutablePaginationToken(pageSize: 2),
        };

        T? result = null;
        await foreach (var item in this.EnumerateAsync(query, cancellationToken, multiReadOptions))
            result = result is null ? item : throw new IOException($"Failed to load {typeof(T).Name} because multiple results were found");
        return result;
    }

    public async Task<IReadOnlyList<T>> ListAsync<T>(Func<IOrderedQueryable<T>, IQueryable<T>> query, CancellationToken cancellationToken, MultiReadOptions? options = null)
        where T : IPocEntity
    {
        var result = new List<T>();
        await foreach (var item in this.EnumerateAsync(query, cancellationToken, options))
            result.Add(item);
        return result;
    }

    public async IAsyncEnumerable<T> EnumerateAsync<T>(Func<IOrderedQueryable<T>, IQueryable<T>> query, [EnumeratorCancellation] CancellationToken cancellationToken, MultiReadOptions? options = null)
        where T : IPocEntity
    {
        options ??= MultiReadOptions.Default;

        var queryable = this.Container.GetItemLinqQueryable<T>(
            allowSynchronousQueryExecution: false,
            continuationToken: options.PaginationToken?.ContinuationToken,
            requestOptions: new QueryRequestOptions()
            {
                EnableScanInQuery = false,
                ConsistencyLevel = options.FullyConsistent ? ConsistencyLevel.BoundedStaleness : null, // BoundedStaleness in a single region is strongly consistent, at the cost of double reads
                MaxItemCount = options.PaginationToken?.PageSize,
                PartitionKey = options.PartitionKey is null ? null : new PartitionKey(options.PartitionKey.Value),
                //SessionToken = "", // TODO Enhancement: Pass session token manually? https://github.com/Azure/azure-cosmos-dotnet-v3/discussions/4237
            });

        var instruction = query(queryable);

        // Protect against developer error: require a significant property to be used, so as to filter out the correct type of entity
        System.Diagnostics.Debug.Assert(
            GetRegexOfQueryAddressingEntityProperty().IsMatch(instruction.ToQueryDefinition().QueryText) ||
            instruction.ToQueryDefinition().QueryText.Contains(@"root[""id""]"),
            "A filter, ordering, or grouping on one of the entity's properties is required, to avoid matching unrelated types. Consider using a filter that is always true if the member exists.");

        using var iterator = instruction.ToFeedIterator();

        // Get an initial data set
        var resultCount = 0;
        string? continuationToken;
        do
        {
            var response = await iterator.ReadNextAsync(cancellationToken);

            resultCount += response.Count;
            continuationToken = response.ContinuationToken;

            foreach (var result in response)
                yield return result;
        } while (iterator.HasMoreResults && options.PaginationToken is null); // If not paginated, iterate until exhausted

        // Common case: no pagination
        // We have already iterated to return all data, so we are done
        if (options.PaginationToken is not MutablePaginationToken paginationToken)
            yield break;

        // Less common case: pagination fulfilled
        // We are done, once we update the caller's continuation token
        if (resultCount >= paginationToken.PageSize || !iterator.HasMoreResults)
        {
            paginationToken.ContinuationToken = continuationToken;
            yield break;
        }

        // Rare case: pagination not fulfilled in one go (because server may choose to return fewer items per iteration)
        // Recurse to get the remaining items (without getting too many), and update the caller's continuation token once we have fully succeeded
        var paginationTokenCopy = paginationToken with
        {
            ContinuationToken = continuationToken,
            PageSize = (ushort)(paginationToken.PageSize - resultCount),
        };
        await foreach (var result in this.EnumerateAsync(query, cancellationToken, options with { PaginationToken = paginationTokenCopy }))
            yield return result;
        paginationToken.ContinuationToken = paginationTokenCopy.ContinuationToken;
    }

    public ValueTask<StorageTransaction> CreateTransactionAsync(DataPartitionKey partitionKey, CancellationToken cancellationToken)
    {
        var result = new CosmosTransaction(
            partitionKey: partitionKey,
            repository: this,
            cancellationToken: cancellationToken);
        return new ValueTask<StorageTransaction>(result);
    }

    public ValueTask<StorageTransaction> AddAsync(IPocEntity entity, StorageTransaction transaction, CancellationToken cancellationToken)
    {
        if (entity is null)
            throw new ArgumentNullException(nameof(entity));

        transaction.AddOperationToBatch(entity, batch => batch.CreateItem(entity));

        return ValueTask.FromResult(transaction);
    }

    public ValueTask<StorageTransaction> AddRangeAsync(IEnumerable<IPocEntity> entities, StorageTransaction transaction, CancellationToken cancellationToken)
    {
        if (entities is null)
            throw new ArgumentNullException(nameof(entities));

        foreach (var entity in entities)
            transaction.AddOperationToBatch(entity, batch => batch.CreateItem(entity));

        return ValueTask.FromResult(transaction);
    }

    public ValueTask<StorageTransaction> UpdateAsync(IPocEntity entity, StorageTransaction transaction, CancellationToken cancellationToken, ModificationOptions? options = null)
    {
        if (entity is null)
            throw new ArgumentNullException(nameof(entity));

        options ??= ModificationOptions.Default;

        transaction.AddOperationToBatch(entity, batch => batch.ReplaceItem(entity.GetId().ToString(), entity, new TransactionalBatchItemRequestOptions()
        {
            IfMatchEtag = options.IgnoresConcurrencyProtection ? null : entity.ETag, // Optimistic concurrency protection
        }));

        return ValueTask.FromResult(transaction);
    }

    public ValueTask<StorageTransaction> DeleteAsync(IPocEntity entity, StorageTransaction transaction, CancellationToken cancellationToken, ModificationOptions? options = null)
    {
        if (entity is null)
            throw new ArgumentNullException(nameof(entity));

        options ??= ModificationOptions.Default;

        transaction.AddOperationToBatch(entity: null, batch => batch.DeleteItem(entity.GetId().ToString(), new TransactionalBatchItemRequestOptions()
        {
            IfMatchEtag = options.IgnoresConcurrencyProtection ? null : entity.ETag, // Optimistic concurrency protection
        }));

        return ValueTask.FromResult(transaction);
    }

    public ValueTask<StorageTransaction> DeleteAsync(string id, StorageTransaction transaction, CancellationToken cancellationToken, ModificationOptions? options = null)
    {
        if (id is null)
            throw new ArgumentNullException(nameof(id));

        if (options?.IgnoresConcurrencyProtection != true)
            throw new InvalidOperationException($"Without an entity instance (with an etag), concurrency protection is impossible. Pass {nameof(ModificationOptions)}.{nameof(ModificationOptions.IgnoresConcurrencyProtection)}=true.");

        transaction.AddOperationToBatch(entity: null, batch => batch.DeleteItem(id));

        return ValueTask.FromResult(transaction);
    }

    public Task CommitAsync(StorageTransaction transaction, CancellationToken cancellationToken)
    {
        return transaction.CommitAsync(cancellationToken).AsTask();
    }

    public Task RollBackAsync(StorageTransaction transaction, CancellationToken cancellationToken)
    {
        return transaction.RollBackAsync(cancellationToken).AsTask();
    }

    [GeneratedRegex(@"root\[""\w+_")] // As in: root["Type_PropName"]
    private static partial Regex GetRegexOfQueryAddressingEntityProperty();
}
