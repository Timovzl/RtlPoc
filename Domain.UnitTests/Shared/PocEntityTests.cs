namespace Rtl.News.RtlPoc.Domain.UnitTests.Shared;

public sealed class PocEntityTests
{
	[Fact]
	public void Equals_Regularly_ShouldUseIdEquality()
	{
		var one = new TestEntity(IdGenerator.CreateId(), "Jan");
		var two = new TestEntity(IdGenerator.CreateId(), "Piet");
		var renamedOne = new TestEntity(one.Id, "Jan2");

		one.Equals(one).ShouldBeTrue();
		two.Equals(two).ShouldBeTrue();
		one.Equals(two).ShouldBeFalse();
		one.Equals(renamedOne).ShouldBeTrue(); // Different object, even with changed properties, but same ID
	}

	[Fact]
	public void GetHashCode_Regularly_ShouldUseId()
	{
		var one = new TestEntity(IdGenerator.CreateId(), "Jan");
		var two = new TestEntity(IdGenerator.CreateId(), "Piet");
		var renamedOne = new TestEntity(one.Id, "Jan2");

		one.GetHashCode().Equals(one.GetHashCode()).ShouldBeTrue();
		two.GetHashCode().Equals(two.GetHashCode()).ShouldBeTrue();
		one.GetHashCode().Equals(two.GetHashCode()).ShouldBeFalse();
		one.GetHashCode().Equals(renamedOne.GetHashCode()).ShouldBeTrue(); // Different object, even with changed properties, but same ID
	}

	[Fact]
	public void PartitionKey_Regularly_ShouldBeLast3CharsOfId()
	{
		var instance = new TestEntity(IdGenerator.CreateId(), "Jan");
		var id = instance.Id;

		var result = instance.PartitionKey;

		result.Value.ShouldBe(id.Value[^3..]);
	}
}

[IdentityValueObject<string>]
internal partial record struct TestId;
[Entity]
internal sealed class TestEntity(
	string id,
	string name)
	: PocEntity<TestId>(id)
{
	public string Name { get; set; } = name;
}
