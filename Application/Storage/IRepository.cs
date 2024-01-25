using Architect.DomainModeling;

namespace Rtl.News.RtlPoc.Application.Storage;

/// <summary>
/// General storage repository for core data.
/// </summary>
public interface IRepository
{
    /// <summary>
    /// Retrieves a single entity by ID, or null if it does not exist.
    /// </summary>
    Task<T?> GetAsync<T>(IIdentity<string> id, CancellationToken cancellationToken, ReadOptions? options = null)
        where T : IPocEntity
    {
        var idString = id.ToString()!;
        return GetAsync<T>(idString, (DataPartitionKey)idString, cancellationToken, options);
    }

    /// <summary>
    /// <para>
    /// Retrieves a single entity by ID, or null if it does not exist.
    /// </para>
    /// <para>
    /// Because this method accepts arbitrary string IDs, unlike the other overload, it is up to the caller to extract the partition key.
    /// There is no performance difference.
    /// </para>
    /// </summary>
    Task<T?> GetAsync<T>(string id, DataPartitionKey partitionKey, CancellationToken cancellationToken, ReadOptions? options = null)
        where T : IPocEntity;

    /// <summary>
    /// <para>
    /// Checks for the existence of any entities matching a query.
    /// </para>
    /// <para>
    /// If the ID is known, instead simply obtain the entity using <see cref="Get{T}(Guid, CancellationToken)"/>.
    /// </para>
    /// </summary>
    /// <param name="query">The query identifying the entities. There must always be a filter on an entity-unique property, to filter the entity type, e.g. 'Where(x => x.Created >= default(DateTimeOffset))'.</param>
    Task<bool> ExistsAsync<T>(Func<IOrderedQueryable<T>, IQueryable<T>> query, CancellationToken cancellationToken, ReadOptions? options = null)
        where T : class, IPocEntity;

    /// <summary>
    /// <para>
    /// Loads a single entity matching a query, or null if there is no match, throwing if multiple entities match.
    /// </para>
    /// <para>
    /// To obtain an entity by ID, instead use <see cref="Get{T}(Guid, CancellationToken)"/>.
    /// </para>
    /// </summary>
    /// <param name="query">The query identifying the entities. There must always be a filter on an entity-unique property, to filter the entity type, e.g. 'Where(x => x.Created >= default(DateTimeOffset))'.</param>
    Task<T?> LoadAsync<T>(Func<IOrderedQueryable<T>, IQueryable<T>> query, CancellationToken cancellationToken, ReadOptions? options = null)
        where T : class, IPocEntity;

    /// <summary>
    /// <para>
    /// Lists the entities matching a query.
    /// </para>
    /// <para>
    /// If many results are expected, instead use <see cref="EnumerateAsync{T}"/> to avoid loading all entities into memory simultaneously.
    /// </para>
    /// </summary>
    /// <param name="query">The query identifying the entities. There must always be a filter on an entity-unique property, to filter the entity type, e.g. 'Where(x => x.Created >= default(DateTimeOffset))'.</param>
    Task<IReadOnlyList<T>> ListAsync<T>(Func<IOrderedQueryable<T>, IQueryable<T>> query, CancellationToken cancellationToken, MultiReadOptions? options = null)
        where T : IPocEntity;

    /// <summary>
    /// <para>
    /// Enumerates the entities matching a query.
    /// </para>
    /// </summary>
    /// <param name="query">The query identifying the entities. There must always be a filter on an entity-unique property, to filter the entity type, e.g. 'Where(x => x.Created >= default(DateTimeOffset))'.</param>
    IAsyncEnumerable<T> EnumerateAsync<T>(Func<IOrderedQueryable<T>, IQueryable<T>> query, CancellationToken cancellationToken, MultiReadOptions? options = null)
        where T : IPocEntity;

    /// <summary>
    /// <para>
    /// Creates a new <see cref="StorageTransaction"/> that can be used to atomically perform up to 100 mutations on a single data partition.
    /// </para>
    /// <para>
    /// Invisible until <see cref="CommitAsync"/>. Rolled back on disposal. Reusable like a new object after commit or rollback.
    /// </para>
    /// </summary>
    ValueTask<StorageTransaction> CreateTransactionAsync(DataPartitionKey partitionKey, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the given <paramref name="entity"/> that does not yet exist in storage.
    /// </summary>
    ValueTask<StorageTransaction> AddAsync(IPocEntity entity, StorageTransaction transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the given <paramref name="entities"/> that do not yet exist in storage.
    /// </summary>
    ValueTask<StorageTransaction> AddRangeAsync(IEnumerable<IPocEntity> entities, StorageTransaction transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Updates the given <paramref name="entity"/> that already exists in storage to match the given state.
    /// </summary>
    ValueTask<StorageTransaction> UpdateAsync(IPocEntity entity, StorageTransaction transaction, CancellationToken cancellationToken, ModificationOptions? options = null);

    /// <summary>
    /// Deletes the given <paramref name="entity"/> from storage if it exists there.
    /// </summary>
    ValueTask<StorageTransaction> DeleteAsync(IPocEntity entity, StorageTransaction transaction, CancellationToken cancellationToken, ModificationOptions? options = null);

    /// <summary>
    /// <para>
    /// Deletes the item with the given <paramref name="id"/> from storage if it exists there.
    /// </para>
    /// <para>
    /// Because no concurrency protection is possible without an entity, this method requires <see cref="ModificationOptions.IgnoresConcurrencyProtection"/> to be passed as <see langword="true"/>, as confirmation.
    /// </para>
    /// </summary>
    ValueTask<StorageTransaction> DeleteAsync(string id, StorageTransaction transaction, CancellationToken cancellationToken, ModificationOptions? options = null);

    /// <summary>
    /// Commits the given <see cref="StorageTransaction"/> to storage.
    /// </summary>
    Task CommitAsync(StorageTransaction transaction, CancellationToken cancellationToken);

    /// <summary>
    /// Rolls back any mutations made for the given <see cref="StorageTransaction"/> that was not yet committed.
    /// </summary>
    Task RollBackAsync(StorageTransaction transaction, CancellationToken cancellationToken);
}
