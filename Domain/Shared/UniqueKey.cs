using System.Linq.Expressions;
using System.Text;
using System.Text.Unicode;

namespace Rtl.News.RtlPoc.Domain.Shared;

/// <summary>
/// Represents a unique key value for a particular property path.
/// </summary>
[ValueObject]
public sealed partial class UniqueKey : IComparable<UniqueKey>
{
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromSeconds(20);

    private const char PathSeparator = '|';

    #region Storage

    /// <summary>
    /// <para>
    /// Calculated property.
    /// </para>
    /// <para>
    /// This concatenation of the <see cref="Path"/> and <see cref="Value"/> enforces uniquenesss in storage.
    /// </para>
    /// </summary>
    [JsonProperty("id")]
    public string Id => $"Uniq{this.Path}{PathSeparator}{this.Value}"; // E.g. Uniq|Migr_Ix|0

    /// <summary>
    /// <para>
    /// Calculated property.
    /// </para>
    /// <para>
    /// Based on the <see cref="Id"/>.
    /// </para>
    /// </summary>
    [JsonProperty("part")]
    public DataPartitionKey PartitionKey => DataPartitionKey.CreateForArbitraryString(this.Value);

    /// <summary>
    /// <para>
    /// Calculated property.
    /// </para>
    /// <para>
    /// How long the item survives in storage before being deleted automatically.
    /// </para>
    /// </summary>
    [JsonProperty("ttl")]
    public ushort TimeToLiveInSeconds => (ushort)DefaultTimeToLive.TotalSeconds;

    #endregion

    [JsonProperty("Uniq_Path")]
    public string Path { get; private init; }

    /// <summary>
    /// UTF-8 bytes encoded in Base64.
    /// </summary>
    [JsonProperty("Uniq_Val")]
    public string Value { get; private init; }

    private UniqueKey(string path, string value)
    {
        this.Path = path ?? throw new ArgumentNullException(nameof(path));
        this.Value = value is null
            ? throw new ArgumentNullException(nameof(value))
            : Base64UrlEncodeValue(value);

        if (this.Path.AsSpan().ContainsAny(DataPartitionKey.SearchValuesForUnsupportedChars))
            throw new ArgumentException($"The path contains unsupported characters: {this.Path}.");

        System.Diagnostics.Debug.Assert(!this.Path.AsSpan().ContainsAny(DataPartitionKey.SearchValuesForUnsupportedChars),
            $"The value contains unsupported chars: {this.Value}.");

        // Our value should be usable as a partition key
        if (!Ascii.IsValid(this.Value) || this.Value.Length > DataPartitionKey.MaxLengthInBytes)
            throw new ArgumentException($"The {nameof(UniqueKey)} is too long with Path={this.Path} and Value={this.Value}. Consider using a WrapperValueObject to constrain the value to 75 or fewer UTF-8 bytes.");
    }

    /// <summary>
    /// <para>
    /// Constructs a unique key value for the given <paramref name="property"/> having the given <paramref name="value"/>.
    /// </para>
    /// <para>
    /// This can be used to represent values for arbitrary unique keys.
    /// </para>
    /// <para>
    /// Example usage:
    /// UniqueKey.Create(() => entity.Data.SomeValue, entity.Data.SomeValue).
    /// </para>
    /// </summary>
    /// <typeparam name="TProperty">The type of the property being resolved. Should be inferred.</typeparam>
    /// <param name="property">An expression representing the path from a local variable to a property, e.g. () => entity.Data.SomeValue.</param>
    /// <param name="value">The same expression, to resolve the value, e.g. entity.Data.SomeValue.</param>
    public static UniqueKey Create<TProperty>(Expression<Func<TProperty>> property, TProperty value)
        where TProperty : notnull
    {
        System.Diagnostics.Debug.Assert(EqualityComparer<TProperty>.Default.Equals(value, property.Compile().Invoke()), "Be sure to use the exact same expression for both parameters.");

        var path = JsonUtilities.GetPropertyPath(property)
            .Replace('/', PathSeparator);

        System.Diagnostics.Debug.Assert(path.StartsWith(PathSeparator));

        var result = new UniqueKey(path: path, value: value?.ToString() ?? "");
        return result;
    }

    /// <summary>
    /// Encodes the value (or up to 2 * <see cref="DataPartitionKey.MaxLengthInBytes"/>, if it is too long) as UTF-8 bytes in Base64Url.
    /// </summary>
    internal static string Base64UrlEncodeValue(string value)
    {
        if (value is null)
            throw new ArgumentNullException(nameof(value));

        Span<byte> bytes = stackalloc byte[2 * DataPartitionKey.MaxLengthInBytes];
        Utf8.FromUtf16(value, bytes, out _, out var bytesWritten);

        Span<char> chars = stackalloc char[bytesWritten / 3 * 4 + 4];
        Convert.TryToBase64Chars(bytes[..bytesWritten], chars, out var charsWritten);

        // Turn Base64 into Base64Url
        while (charsWritten > 0 && chars[charsWritten - 1] == '=')
            charsWritten--;
        chars.Replace('+', '-');
        chars.Replace('/', '_');

        var result = chars[..charsWritten].ToString();
        return result;
    }
}
