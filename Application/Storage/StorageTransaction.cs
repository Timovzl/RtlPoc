namespace Rtl.News.RtlPoc.Application.Storage;

/// <summary>
/// <para>
/// An atomic unit of mutations to data storage.
/// </para>
/// <para>
/// Changes remain invisible outside the current flow of execution until the transaction is committed.
/// They may be visible or invisible to the current flow of execution, depending on the implementation.
/// </para>
/// <para>
/// An uncommitted transaction is rolled back on disposal.
/// </para>
/// </summary>
public abstract class StorageTransaction : IAsyncDisposable
{
    public abstract IRepository Repository { get; }
    public abstract CancellationToken CancellationToken { get; }

    /// <summary>
    /// <para>
    /// Commits the transaction, effectuating its mutations.
    /// </para>
    /// <para>
    /// Regardless of successs or failure, the transaction is reset and can be used like a newly created one.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">An optional token to use instead of the token this object was created with.</param>
    public abstract ValueTask CommitAsync(CancellationToken? cancellationToken = null);

    /// <summary>
    /// <para>
    /// Rolls back the transaction, canceling its mutations.
    /// </para>
    /// <para>
    /// Regardless of successs or failure, the transaction is reset and can be used like a newly created one.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">An optional token to use instead of the token this object was created with.</param>
    public abstract ValueTask RollBackAsync(CancellationToken? cancellationToken = null);

    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return this.RollBackAsync(CancellationToken.None);
    }

    public static implicit operator ValueTask<StorageTransaction>(StorageTransaction transaction) => ValueTask.FromResult(transaction);
}
