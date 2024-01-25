namespace Rtl.News.RtlPoc.Domain;

// TODO: Remove when implementing the first real use case

[IdentityValueObject<string>] public partial record struct ExampleEntityId;

[Entity]
public sealed class ExampleEntity : PocEntity<ExampleEntityId>
{
    [JsonProperty("ExmpEnt_Name")]
    public string Name { get; private init; }

    public ExampleEntity(
        string name)
        : base(IdGenerator.CreateId())
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }
}
