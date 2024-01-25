using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Unicode;

namespace Rtl.News.RtlPoc.Domain.Shared;

/// <summary>
/// A key identifying a data partition.
/// </summary>
[IdentityValueObject<string>]
public readonly partial struct DataPartitionKey
{
    public const ushort MaxLengthInBytes = 100; // CosmosDB limit is 101 (although its code does something different beyond 100) - could be increased to 2048 with a setting, but preferable to avoid

    /// <summary>
    /// <para>
    /// The unsupported characters, on top of various unsupported unicode categories, such as <see cref="UnicodeCategory.Control"/>.
    /// </para>
    /// <para>
    /// Slash and backslash are not permitted in CosmosDB partition keys.
    /// # and ? are disallowed in CosmosDB IDs (though technically allowed in partition keys) and can easily be flagged here.
    /// Double quotes are highly undesirable in any kind of identifier.
    /// </para>
    /// </summary>
    public const string UnsupportedChars = @"/\#?""";
    public static readonly SearchValues<char> SearchValuesForUnsupportedChars = SearchValues.Create(UnsupportedChars);

    /// <summary>
    /// <para>
    /// Creates a partition key for an arbitrary <see cref="String"/> (a few unsupported characters notwithstanding).
    /// </para>
    /// <para>
    /// Do not use this method for regular IDs within this bounded context. Such an ID can simply be cast to a <see cref="DataPartitionKey"/>, which adds useful invariants.
    /// </para>
    /// </summary>
    public static DataPartitionKey CreateForArbitraryString(string notARegularId)
    {
        return new DataPartitionKey(notARegularId);
    }

    /// <summary>
    /// Produces a uniformly random partition key in the preferred format.
    /// </summary>
    public static DataPartitionKey CreateRandom()
    {
        var value = String.Create(length: 3, state: (byte)0, (chars, state) =>
        {
            // Piggyback on the ID encoder for very efficient alphanumeric encoding
            Span<byte> bytes = stackalloc byte[11];
            RandomNumberGenerator.Fill(bytes[..8]);
            AlphanumericIdEncoder.Encode(MemoryMarshal.Cast<byte, ulong>(bytes)[0], bytes);
            Utf8.ToUtf16(bytes[^3..], chars, out _, out _);
        });

        return new DataPartitionKey() { Value = value };
    }

    private DataPartitionKey(string? value)
    {
        this.Value = value ?? "";

        if (Utf8.FromUtf16(this.Value, stackalloc byte[MaxLengthInBytes], out _, out _) == OperationStatus.DestinationTooSmall)
            throw new ValidationException(ErrorCode.PartitionKey_ValueTooLong, $"A {nameof(DataPartitionKey)} must be no longer than {MaxLengthInBytes} characters.");
        if (ContainsNonPrintableOrMultilineCharacters(this.Value) || this.Value.AsSpan().ContainsAny(SearchValuesForUnsupportedChars))
            throw new ValidationException(ErrorCode.PartitionKey_ValueInvalid, $@"A {nameof(DataPartitionKey)} must not contain any of /\""#? and no unprintable or multiline characters.");
    }

    private static bool ContainsNonPrintableOrMultilineCharacters(ReadOnlySpan<char> chars)
    {
        foreach (var chr in chars)
        {
            var category = Char.GetUnicodeCategory(chr);

            if (category is
                UnicodeCategory.LineSeparator or
                UnicodeCategory.ParagraphSeparator or
                UnicodeCategory.Control or
                UnicodeCategory.PrivateUse or
                UnicodeCategory.OtherNotAssigned)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Calculates a <see cref="DataPartitionKey"/> from text.
    /// </summary>
    public static explicit operator DataPartitionKey(string value) => (DataPartitionKey)value.AsSpan();

    /// <summary>
    /// Calculates a <see cref="DataPartitionKey"/> from text.
    /// </summary>
    public static explicit operator DataPartitionKey(ReadOnlySpan<char> value) => value.Length == 22
        ? new DataPartitionKey(value[^3..].ToString()) // Partitioning on the last 3 random alphanumeric chars results in a uniform distribution over 238_328 partitions
        : ThrowUnexpectedValue(); // For non-UUIDs, we cannot simply rely on the last few characters

    [DoesNotReturn]
    private static DataPartitionKey ThrowUnexpectedValue()
    {
        throw new InvalidOperationException($"A {nameof(DataPartitionKey)} can only be directly observed from a 22-char v7 UUID string. For arbitrary strings, use {nameof(CreateForArbitraryString)}() instead.");
    }

    /// <summary>
    /// Indicates whether the given ID belongs to the same partition.
    /// </summary>
    public bool MatchesId(string id)
    {
        return this.Value.Length == 3
            ? id.EndsWith(this.Value)
            : this.Value == id;
    }
}
