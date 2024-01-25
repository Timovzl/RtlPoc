namespace Rtl.News.RtlPoc.Application.Storage;

/// <summary>
/// <para>
/// Represents a pagination set of query operations.
/// </para>
/// <para>
/// As queries are made, the token is mutated to track the progress.
/// </para>
/// </summary>
public record class MutablePaginationToken
{
    public ushort PageSize { get; set; }
    public string? ContinuationToken { get; set; }

    public MutablePaginationToken(ushort pageSize)
    {
        this.PageSize = pageSize;
    }

    public MutablePaginationToken(ushort pageSize, string? continuationToken)
        : this(pageSize: pageSize)
    {
        this.ContinuationToken = continuationToken;
    }
}
