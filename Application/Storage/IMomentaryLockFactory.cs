namespace Rtl.News.RtlPoc.Application.Storage;

/// <summary>
/// Provides <see cref="IMomentaryLock"/> instances.
/// </summary>
public interface IMomentaryLockFactory
{
	/// <summary>
	/// <para>
	/// Acquires a very short-lived global lock on the given <see cref="UniqueKey"/>, waiting for exclusive access if necessary.
	/// </para>
	/// <para>
	/// The lock expires in a small number of seconds or on <see cref="IAsyncDisposable.DisposeAsync"/>, whichever happens first.
	/// </para>
	/// <para>
	/// For example, such a lock can be used to claim unique keys in a distributed fashion, even across unrelated data partitions.
	/// While a lock is held, an existence check plus a potential write of a key can be done <em>without</em> the need for atomicity.
	/// The lock itself need not share a data partition with any entity holding the key.
	/// </para>
	/// <para>
	/// <strong>To hold multiple simultaneous locks, use <see cref="WaitRangeAsync"/> instead.</strong>
	/// Separate invocations of the current methods risk deadlocks and lock expirations.
	/// </para>
	/// </summary>
	Task<IMomentaryLock> WaitAsync(UniqueKey uniqueKey, CancellationToken cancellationToken);

	/// <summary>
	/// <para>
	/// Acquires a very short-lived global lock on the given set of <see cref="UniqueKey"/> items, waiting for exclusive access if necessary.
	/// </para>
	/// <para>
	/// The lock expires in a small number of seconds or on <see cref="IAsyncDisposable.DisposeAsync"/>, whichever happens first.
	/// </para>
	/// <para>
	/// For example, such a lock can be used to claim unique keys in a distributed fashion, even across unrelated data partitions.
	/// While a lock is held, an existence check plus a potential write of a key can be done <em>without</em> the need for atomicity.
	/// The lock itself need not share a data partition with any entity holding the key.
	/// </para>
	/// </summary>
	Task<IMomentaryLock> WaitRangeAsync(IEnumerable<UniqueKey> uniqueKeys, CancellationToken cancellationToken);
}
