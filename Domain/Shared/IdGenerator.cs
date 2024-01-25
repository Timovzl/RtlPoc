using System.Buffers;
using System.Text;

namespace Rtl.News.RtlPoc.Domain.Shared;

public static class IdGenerator
{
    /// <summary>
    /// <para>
    /// Returns a new unique V7 UUID, encoded in alphanumeric (case-sensitive).
    /// </para>
    /// <para>
    /// Inversion is control is possible by calling this method in the presence of an ambient <see cref="DistributedId128GeneratorScope"/>.
    /// </para>
    /// </summary>
    public static string CreateId()
    {
        return CreateIdCore().ToAlphanumeric();
    }

    /// <summary>
    /// <para>
    /// Writes a new unique V7 UUID, encoded in alphanumeric (case-sensitive).
    /// </para>
    /// <para>
    /// Inversion is control is possible by calling this method in the presence of an ambient <see cref="DistributedId128GeneratorScope"/>.
    /// </para>
    /// </summary>
    internal static void CreateId(Span<byte> outputBytes)
    {
        CreateIdCore().ToAlphanumeric(outputBytes);
    }

    private static Guid CreateIdCore()
    {
        return DistributedId128.CreateGuid();
    }

    /// <summary>
    /// Returns a new ID whose partition matches the given <paramref name="partitionKey"/>.
    /// </summary>
    public static string CreateIdInPartition(DataPartitionKey partitionKey)
    {
        if (partitionKey.Value.Length != 3)
            throw new InvalidOperationException($"Targeted partitioning can only be used with a {nameof(DataPartitionKey)} that was cast directly from a 22-char v7 UUID string.");

        var result = String.Create(22, partitionKey, static (chars, existingPartitionKey) =>
        {
            // Generate a new ID
            Span<byte> idBytes = stackalloc byte[22];
            CreateId(idBytes);

            // Write it into the new string
            Ascii.ToUtf16(idBytes, chars, out _);

            // Overwrite the last 3 chars (~18 bits worth of data) with the current partition key
            // These chars form the partition key
            // Dropping ~18 bits of randomness is fine:
            // Either the ID contained 75 random bits, or it had received a 58-bit random increment since its predecessor (of which 40 bits will remain)
            existingPartitionKey.Value.AsSpan().CopyTo(chars[^3..]);
        });
        return result;
    }

    /// <summary>
    /// <para>
    /// Returns a disposable object in whose scope all ID generation is ambiently changed to generate IDs that have the same partition.
    /// </para>
    /// <para>
    /// Any potential outer scopes (such as from unit tests) are honored. The last few characters of each generated ID are overwritten.
    /// </para>
    /// </summary>
    /// <param name="partitionKey">The <see cref="DataPartitionKey"/> to use, or null to generate a uniformly random one.</param>
    public static DistributedId128GeneratorScope CreateIdGeneratorScopeForSinglePartition(DataPartitionKey? partitionKey = null)
    {
        return new DistributedId128GeneratorScope(new FixedPartitionIdGenerator(partitionKey));
    }
}

file sealed class FixedPartitionIdGenerator : IDistributedId128Generator
{
    /// <summary>
    /// The ID generator that was ambient when we were constructed, i.e. before we overrode it as the ambient one.
    /// </summary>
    private readonly IDistributedId128Generator _parentGenerator = DistributedId128GeneratorScope.CurrentGenerator;

    private readonly DataPartitionKey _partitionKey;
    private readonly bool _hasRandomPartitionKey;

    public FixedPartitionIdGenerator(DataPartitionKey? partitionKey)
    {
        this._hasRandomPartitionKey = partitionKey is null;
        this._partitionKey = partitionKey ?? DataPartitionKey.CreateRandom();

        if (this._partitionKey.Value.Length != 3)
            throw new InvalidOperationException($"Targeted partitioning can only be used with a {nameof(DataPartitionKey)} that was cast directly from a 22-char v7 UUID string.");
    }

    public Guid CreateGuid()
    {
        return this.CreateId().ToGuid();
    }

    public UInt128 CreateId()
    {
        // Generate a new ID
        var id = this._parentGenerator.CreateId();

        // If we are being controlled by a test, and no deliberate partition key was used, then stick to what the test dictates
        // This helps obtain predictable IDs in tests
        if (this._parentGenerator.GetType().Name == "IncrementalIdGenerator" && this._hasRandomPartitionKey)
            return id;

        // Convert it to alphanumeric chars
        Span<byte> chars = stackalloc byte[22];
        AlphanumericIdEncoder.Encode(id, chars);

        // We use V7 UUIDs in alphanumeric (base62) form
        // These end in a lot of randomness
        // Partitioning on the last 3 random characters (~18 bits of entropy) results in a uniform distribution over 238328 partitions
        // Additionally, this makes it possible to put things in the same partition by deliberately assigning those characters
        // Overwriting the last ~18 bits does not significantly hinder the UUID scheme's properties:
        // Usually an ID contained 75 random bits, and occasionally one had received a 58-bit random increment since its predecessor (of which 40 bits will remain)
        var partitionKeySpan = this._partitionKey.Value.AsSpan();
        if (Ascii.FromUtf16(partitionKeySpan, chars[^3..], out var bytesWritten) != OperationStatus.Done || bytesWritten != 3)
            throw new InvalidOperationException($"'{this._partitionKey}' is an unsuitable partition key for creating IDs in the same partition. Use one based on IDs created by the {nameof(IdGenerator)}.");

        // Decode the resulting ID
        AlphanumericIdEncoder.TryDecodeUInt128(chars, out var result);
        return result;
    }
}
