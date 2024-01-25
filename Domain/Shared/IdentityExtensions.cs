namespace Rtl.News.RtlPoc.Domain.Shared;

/// <summary>
/// Provides extension methods on <see cref="IIdentity{T}"/>.
/// </summary>
public static class IdentityExtensions
{
    /// <summary>
    /// Returns the <see cref="DataPartitionKey"/> represented by the given <paramref name="id"/>.
    /// </summary>
    public static DataPartitionKey GetPartitionKey(IIdentity<string> id)
    {
        return (DataPartitionKey)id;
    }
}
