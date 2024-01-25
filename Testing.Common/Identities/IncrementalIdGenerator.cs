using Architect.Identities;
using Rtl.News.RtlPoc.Domain.Shared;

namespace Rtl.News.RtlPoc.Testing.Common;

/// <summary>
/// <para>
/// An ID generator for testing that generates incremental IDs: "0000000000100000000par", "0000000000200000000par", "0000000000300000000par", and so on.
/// </para>
/// <para>
/// The final characters are determined by the <see cref="DataPartitionKey"/> passed on creation.
/// </para>
/// </summary>
public sealed class IncrementalIdGenerator : IDistributedId128Generator
{
    private readonly DataPartitionKey _partitionKey;
    private readonly ulong _partitionKeyNumericValue;

    private ulong _previousIncrement = 0;

    /// <param name="partitionKey">The partition key that determines the last few characters, or null to use "par".</param>
    public IncrementalIdGenerator(DataPartitionKey? partitionKey = null)
    {
        _partitionKey = partitionKey ?? DataPartitionKey.CreateForArbitraryString("par");

        if (_partitionKey.Value.Length != 3)
            throw new ArgumentException("The partition key should have a length of 3 characters.");

        _partitionKeyNumericValue = (ulong)AlphanumericIdEncoder.DecodeLongOrDefault($"00000000{_partitionKey.Value}")!.Value;
    }

    public Guid CreateGuid()
    {
        return CreateId().ToGuid();
    }

    public UInt128 CreateId()
    {
        var id = (UInt128)Interlocked.Increment(ref _previousIncrement) << 64;
        id |= _partitionKeyNumericValue;
        return id;
    }
}
