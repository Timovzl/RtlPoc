namespace Rtl.News.RtlPoc.Application.Storage;

/// <summary>
/// <para>
/// Represents a very short-lived lock on a shared resource.
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
public interface IMomentaryLock : IAsyncDisposable
{
}
