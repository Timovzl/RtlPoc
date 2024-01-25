namespace Rtl.News.RtlPoc.Domain.Shared;

/// <summary>
/// Any domain entity used in <see cref="RtlPoc"/>.
/// </summary>
public abstract class PocEntity<TId> : Entity, IPocEntity
    where TId : IIdentity<string>, ISerializableDomainObject<TId, string>, IEquatable<TId>, IComparable<TId>
{
    public override string ToString() => $"{{{GetType().Name} Id={Id}}}";
    public sealed override int GetHashCode() => Id.GetHashCode();
    public sealed override bool Equals(object? obj) => obj is PocEntity<TId> other && Equals(other);
    public bool Equals(PocEntity<TId>? other) => other is not null && Id.Equals(other.Id);

    [JsonProperty("id")] // Exact casing required for CosmosDB
    public TId Id { get; private init; }

    string IPocEntity.GetId()
    {
        return Id.ToString()!;
    }

    /// <summary>
    /// Obtained from the <see cref="Id"/>.
    /// </summary>
    [JsonProperty("part")]
    public virtual DataPartitionKey PartitionKey => _partitionKey ??= (DataPartitionKey)Id.ToString()!;
    private DataPartitionKey? _partitionKey;

    [JsonProperty("_etag")]
    public string? ETag { get; set; }

    protected PocEntity(TId id)
    {
        Id = id;
    }
}

public interface IPocEntity
{
    /// <summary>
    /// Allows the ID to be obtained without knowledge of the concrete type.
    /// </summary>
    string GetId();

    /// <summary>
    /// Entity tag used for version control.
    /// </summary>
    public string? ETag { get; set; }
}
