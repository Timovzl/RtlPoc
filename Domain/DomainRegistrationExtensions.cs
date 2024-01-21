using Architect.DomainModeling.Configuration;
using Architect.DomainModeling.Conversions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace Rtl.News.RtlPoc.Domain;

public static class DomainRegistrationExtensions
{
	public static IServiceCollection AddDomainLayer(this IServiceCollection services, IConfiguration _)
	{
		// Register constructorless deserialization for entities, to keep us from needing to write "unused" default constructors
		var jsonConversionConfigurator = new JsonConversionConfigurator();
		EntityDomainModelConfigurator.ConfigureEntities(jsonConversionConfigurator);
		JsonConvert.DefaultSettings = () => new JsonSerializerSettings()
		{
			Converters = jsonConversionConfigurator.JsonConverters,
		};

		// Register the current project's dependencies
		services.Scan(scanner => scanner.FromAssemblies(typeof(DomainRegistrationExtensions).Assembly)
			.AddClasses(c => c.Where(type => type.GetInterface(typeof(IDomainService).FullName!) is not null), publicOnly: false)
			.AsSelfWithInterfaces().WithSingletonLifetime());

		return services;
	}

	private sealed class JsonConversionConfigurator : IEntityConfigurator
	{
		public List<JsonConverter> JsonConverters { get; } = [];

		public void ConfigureEntity<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] TEntity>(
			in IEntityConfigurator.Args args)
			where TEntity : IEntity
		{
			this.JsonConverters.Add(new ConstructorlessJsonConverter<TEntity>());
		}
	}

	private sealed class ConstructorlessJsonConverter<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)] T>
		: JsonConverter<T>
		where T : IDomainObject
	{
		public override bool CanWrite => false;
		public override bool CanRead => true;

		public override void WriteJson(JsonWriter writer, T? value, JsonSerializer serializer)
		{
			throw new NotSupportedException("This type is only used for deserialization.");
		}

		public override T? ReadJson(JsonReader reader, Type objectType, T? existingValue, bool hasExistingValue, JsonSerializer serializer)
		{
			if (reader.TokenType == JsonToken.Null)
				return default;

			var result = DomainObjectSerializer.Deserialize<T>();
			serializer.Populate(reader, result);
			return result;
		}
	}
}
