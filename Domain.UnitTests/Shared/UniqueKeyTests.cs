using Newtonsoft.Json;

namespace Rtl.News.RtlPoc.Domain.UnitTests.Shared;

public sealed class UniqueKeyTests
{
	private sealed class SerializableTestType
	{
		public readonly int Field = 1;
		public int Property => 1;
		public int Method() => 1;

		[JsonProperty("SeriTest_IntJsonProp")]
		public int IntJsonProperty => 1;

		[JsonProperty("SeriTest_StringJsonProp")]
		public string StringJsonProperty { get; init; } = @"/\#?""";

		[JsonProperty("SeriTest_NullJsonProperty")]
		public string? NullJsonProperty => null;
		[JsonProperty("SeriTest_ZeroCharJsonProperty")]
		public string ZeroCharJsonProperty => "";
		[JsonProperty("SeriTest_OneCharJsonProperty")]
		public string OneCharJsonProperty => "0";
		[JsonProperty("SeriTest_TwoCharJsonProperty")]
		public string TwoCharJsonProperty => "00";
		[JsonProperty("SeriTest_ThreeCharJsonProperty")]
		public string ThreeCharJsonProperty => "000";
		[JsonProperty("SeriTest_FourCharJsonProperty")]
		public string FourCharJsonProperty => "0000";

		[JsonProperty("SeriTest_Nested")]
		public SerializableTestType Nested => this._nested ??= new SerializableTestType();
		private SerializableTestType? _nested;
	}

	[Fact]
	public void Create_WithoutPropertyMemberExpression_ShouldThrow()
	{
		Exception exception;
		var obj = new SerializableTestType();

		// Method
		exception = Should.Throw<ArgumentException>(() => UniqueKey.Create(() => obj.Method(), obj.Method()));
		exception.Message.ShouldContain("member", Case.Insensitive);

		// Field
		exception = Should.Throw<ArgumentException>(() => UniqueKey.Create(() => obj.Field, obj.Field));
		exception.Message.ShouldContain("member", Case.Insensitive);

		// Constant expression
		exception = Should.Throw<ArgumentException>(() => UniqueKey.Create(() => 1, 1));
		exception.Message.ShouldContain("member", Case.Insensitive);
	}

	[Fact]
	public void Create_WithoutJsonPropertyAttribute_ShouldThrow()
	{
		var obj = new SerializableTestType();

		var exception = Should.Throw<ArgumentException>(() => UniqueKey.Create(() => obj.Property, obj.Property));
		exception.Message.ShouldContain("Attribute", Case.Insensitive);
	}

	[Fact]
	public void Create_WithSimpleProperty_ShouldYieldExpectedResult()
	{
		var obj = new SerializableTestType();

		var result = UniqueKey.Create(() => obj.IntJsonProperty, obj.IntJsonProperty);

		result.Path.ShouldBe("|SeriTest_IntJsonProp");
		result.Value.ShouldBe("MQ"); // "1" in Base64Url
		result.Id.ShouldBe("Uniq|SeriTest_IntJsonProp|MQ");
		result.PartitionKey.Value.ShouldBe(result.Value);
		result.TimeToLiveInSeconds.ShouldBe((ushort)20);
	}

	[Fact]
	public void GetPropertyPath_WithNestedProperty_ShouldYieldExpectedResult()
	{
		var obj = new SerializableTestType();

		var result = UniqueKey.Create(() => obj.Nested.StringJsonProperty, obj.Nested.StringJsonProperty);

		result.Path.ShouldBe("|SeriTest_Nested|SeriTest_StringJsonProp");
		result.Value.ShouldBe("L1wjPyI"); // /\#?"" in Base64Url
		result.Id.ShouldBe("Uniq|SeriTest_Nested|SeriTest_StringJsonProp|L1wjPyI");
		result.PartitionKey.Value.ShouldBe(result.Value);
		result.TimeToLiveInSeconds.ShouldBe((ushort)20);
	}

	[Fact]
	public void Value_WithNullInput_ShouldYieldExpectedResult()
	{
		var obj = new SerializableTestType();
		var instance = UniqueKey.Create(() => obj.NullJsonProperty!, obj.NullJsonProperty!);

		instance.Value.ShouldBe("");
	}

	[Fact]
	public void Value_WithZeroCharInput_ShouldYieldExpectedResult()
	{
		var obj = new SerializableTestType();
		var instance = UniqueKey.Create(() => obj.ZeroCharJsonProperty, obj.ZeroCharJsonProperty);

		instance.Value.ShouldBe("");
	}

	[Fact]
	public void Value_WithOneCharInput_ShouldYieldExpectedResult()
	{
		var obj = new SerializableTestType();
		var instance = UniqueKey.Create(() => obj.OneCharJsonProperty, obj.OneCharJsonProperty);

		instance.Value.ShouldBe("MA"); // "0" in Base64Url
	}

	[Fact]
	public void Value_WithTwoCharInput_ShouldYieldExpectedResult()
	{
		var obj = new SerializableTestType();
		var instance = UniqueKey.Create(() => obj.TwoCharJsonProperty, obj.TwoCharJsonProperty);

		instance.Value.ShouldBe("MDA"); // "00" in Base64Url
	}

	[Fact]
	public void Value_WithThreeCharInput_ShouldYieldExpectedResult()
	{
		var obj = new SerializableTestType();
		var instance = UniqueKey.Create(() => obj.ThreeCharJsonProperty, obj.ThreeCharJsonProperty);

		instance.Value.ShouldBe("MDAw"); // "000" in Base64Url
	}

	[Fact]
	public void Value_WithFourCharInput_ShouldYieldExpectedResult()
	{
		var obj = new SerializableTestType();
		var instance = UniqueKey.Create(() => obj.FourCharJsonProperty, obj.FourCharJsonProperty);

		instance.Value.ShouldBe("MDAwMA"); // "0000" in Base64Url
	}

	[Fact]
	public void Value_WithSpecialCharInBase64Output_ShouldUseUrlVariant()
	{
		var obj = new SerializableTestType() { StringJsonProperty = "💩" };
		var instance = UniqueKey.Create(() => obj.StringJsonProperty, obj.StringJsonProperty);

		instance.Value.ShouldBe("8J-SqQ"); // '-' rather than '+' proves Base64Url (rather than regular Base64)
	}
}
