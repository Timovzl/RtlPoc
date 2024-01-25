using System.Text;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Rtl.News.RtlPoc.Infrastructure.Databases.Shared;

// Copied from Microsoft with minimal adjustments, so we need to suppress some analyzers
#nullable disable
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0063 // Use simple 'using' statement
#pragma warning disable CA1859 // Use concrete types when possible for improved performance

/// <summary>
/// <para>
/// A copy of the default Cosmos JSON.NET serializer that also honors <see cref="JsonConvert.DefaultSettings"/>.
/// </para>
/// <para>
/// The default serializer was obtained on 2024-01-01 (with its most recent edits ~5 years old) from https://github.com/Azure/azure-cosmos-dotnet-v3/blob/master/Microsoft.Azure.Cosmos/src/Serializer/CosmosJsonDotNetSerializer.cs.
/// </para>
/// </summary>
internal sealed class CosmosNewtonsoftSerializerHonoringDefaults : CosmosSerializer
{
    private static readonly Encoding DefaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private JsonSerializerSettings SerializerSettings { get; }

    /// <summary>
    /// Create a serializer that uses the JSON.net serializer
    /// </summary>
    /// <remarks>
    /// This is internal to reduce exposure of JSON.net types so
    /// it is easier to convert to System.Text.Json
    /// </remarks>
    internal CosmosNewtonsoftSerializerHonoringDefaults()
    {
        this.SerializerSettings = JsonConvert.DefaultSettings?.Invoke() ?? new JsonSerializerSettings();
    }

    /// <summary>
    /// Create a serializer that uses the JSON.net serializer
    /// </summary>
    /// <remarks>
    /// This is internal to reduce exposure of JSON.net types so
    /// it is easier to convert to System.Text.Json
    /// </remarks>
    internal CosmosNewtonsoftSerializerHonoringDefaults(CosmosSerializationOptions cosmosSerializerOptions)
    {
        if (cosmosSerializerOptions == null)
        {
            this.SerializerSettings = JsonConvert.DefaultSettings();
            return;
        }

        JsonSerializerSettings jsonSerializerSettings = JsonConvert.DefaultSettings();

        jsonSerializerSettings.NullValueHandling = cosmosSerializerOptions.IgnoreNullValues ? NullValueHandling.Ignore : NullValueHandling.Include;
        jsonSerializerSettings.Formatting = cosmosSerializerOptions.Indented ? Formatting.Indented : Formatting.None;
        jsonSerializerSettings.ContractResolver = cosmosSerializerOptions.PropertyNamingPolicy == CosmosPropertyNamingPolicy.CamelCase
                ? new CamelCasePropertyNamesContractResolver()
                : null;
        jsonSerializerSettings.MaxDepth = 64; // https://github.com/advisories/GHSA-5crp-9r3c-p9vr

        this.SerializerSettings = jsonSerializerSettings;
    }

    /// <summary>
    /// Create a serializer that uses the JSON.net serializer
    /// </summary>
    /// <remarks>
    /// This is internal to reduce exposure of JSON.net types so
    /// it is easier to convert to System.Text.Json
    /// </remarks>
    internal CosmosNewtonsoftSerializerHonoringDefaults(JsonSerializerSettings jsonSerializerSettings)
    {
        this.SerializerSettings = jsonSerializerSettings ?? throw new ArgumentNullException(nameof(jsonSerializerSettings));
    }

    /// <summary>
    /// Convert a Stream to the passed in type.
    /// </summary>
    /// <typeparam name="T">The type of object that should be deserialized</typeparam>
    /// <param name="stream">An open stream that is readable that contains JSON</param>
    /// <returns>The object representing the deserialized stream</returns>
    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            using (StreamReader sr = new StreamReader(stream))
            {
                using (JsonTextReader jsonTextReader = new JsonTextReader(sr))
                {
                    JsonSerializer jsonSerializer = this.GetSerializer();
                    return jsonSerializer.Deserialize<T>(jsonTextReader);
                }
            }
        }
    }

    /// <summary>
    /// Converts an object to a open readable stream
    /// </summary>
    /// <typeparam name="T">The type of object being serialized</typeparam>
    /// <param name="input">The object to be serialized</param>
    /// <returns>An open readable stream containing the JSON of the serialized object</returns>
    public override Stream ToStream<T>(T input)
    {
        MemoryStream streamPayload = new MemoryStream();
        using (StreamWriter streamWriter = new StreamWriter(streamPayload, encoding: DefaultEncoding, bufferSize: 1024, leaveOpen: true))
        {
            using (JsonWriter writer = new JsonTextWriter(streamWriter))
            {
                writer.Formatting = Formatting.None;
                JsonSerializer jsonSerializer = this.GetSerializer();
                jsonSerializer.Serialize(writer, input);
                writer.Flush();
                streamWriter.Flush();
            }
        }

        streamPayload.Position = 0;
        return streamPayload;
    }

    /// <summary>
    /// JsonSerializer has hit a race conditions with custom settings that cause null reference exception.
    /// To avoid the race condition a new JsonSerializer is created for each call
    /// </summary>
    private JsonSerializer GetSerializer()
    {
        return JsonSerializer.Create(this.SerializerSettings);
    }
}
