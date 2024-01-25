namespace Rtl.News.RtlPoc.Application.Storage;

/// <summary>
/// Abstract options for performing storage operations.
/// </summary>
public abstract record class StorageOptions
{
}

/// <summary>
/// Options for reading items from storage.
/// </summary>
public record class ReadOptions : StorageOptions
{
    public static ReadOptions Default { get; } = new ReadOptions();

    /// <summary>
    /// <para>
    /// Causes <em>only</em> the given partition to be inspected.
    /// </para>
    /// <para>
    /// If the partition key is known for certain, set this for improved performance.
    /// </para>
    /// <para>
    /// Unnecessary for Get (by ID) operations.
    /// </para>
    /// </summary>
    public DataPartitionKey? PartitionKey { get; init; }

    /// <summary>
    /// <para>
    /// Enforces consistency beyond just the current session, at the cost of more expensive reads.
    /// </para>
    /// <para>
    /// Set to true if stale data from outside mutations cannot be tolerated.
    /// </para>
    /// </summary>
    public bool FullyConsistent { get; init; }
}

/// <summary>
/// Options for reading a sequence of items from storage.
/// </summary>
public record class MultiReadOptions : ReadOptions
{
    public static new MultiReadOptions Default { get; } = new MultiReadOptions();

    /// <summary>
    /// <para>
    /// To use pagination, set a token, whose <see cref="MutablePaginationToken.ContinuationToken"/> property will then be overwritten.
    /// </para>
    /// <para>
    /// Use the updated token when obtaining the next page.
    /// </para>
    /// <para>
    /// This pagination does <em>not</em> provide snapshot consistency.
    /// However, it <em>does</em> avoid two common pagination pitfalls:
    /// (A) results duplicated across pages; (B) skipped results caused by deletions in previous pages.
    /// </para>
    /// </summary>
    public MutablePaginationToken? PaginationToken { get; init; }

    public MultiReadOptions()
    {
    }

    public MultiReadOptions(ReadOptions? readOptions)
        : this()
    {
        if (readOptions is null)
            return;

        this.PartitionKey = readOptions.PartitionKey;
        this.FullyConsistent = readOptions.FullyConsistent;
    }
}

/// <summary>
/// Options for performing modifications to existing data in storage.
/// </summary>
public record class ModificationOptions : StorageOptions
{
    public static ModificationOptions Default { get; } = new ModificationOptions();

    /// <summary>
    /// <para>
    /// Set to true to perform the modification even if another process has mutated the entity since it was read.
    /// </para>
    /// <para>
    /// The result for an update to an entity deleted in the meantime is undefined.
    /// </para>
    /// </summary>
    public bool IgnoresConcurrencyProtection { get; init; }
}
