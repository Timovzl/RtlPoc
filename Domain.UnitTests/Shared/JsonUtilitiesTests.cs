using Newtonsoft.Json;

namespace Rtl.News.RtlPoc.Domain.UnitTests.Shared;

public sealed class JsonUtilitiesTests
{
	private sealed class SerializableTestType
	{
		public static int StaticProperty => 1;

		public readonly int Field = 1;
		public int Property => 1;
		public int Method() => 1;

		[JsonProperty("SeriTest_JsonProp")]
		public int JsonProperty => 1;

		[JsonProperty("SeriTest_Nested")]
		public SerializableTestType? Nested { get; }
	}

	[Fact]
	public void GetPropertyPath_WithoutPropertyMemberExpression_ShouldThrow()
	{
		Exception exception;
		var obj = new SerializableTestType();

		// Method
		exception = Should.Throw<ArgumentException>(() => JsonUtilities.GetPropertyPath((SerializableTestType obj) => obj.Method()));
		exception.Message.ShouldContain("member", Case.Insensitive);
		exception = Should.Throw<ArgumentException>(() => JsonUtilities.GetPropertyPath(() => obj.Method()));
		exception.Message.ShouldContain("member", Case.Insensitive);

		// Field
		exception = Should.Throw<ArgumentException>(() => JsonUtilities.GetPropertyPath((SerializableTestType obj) => obj.Field));
		exception.Message.ShouldContain("member", Case.Insensitive);
		exception = Should.Throw<ArgumentException>(() => JsonUtilities.GetPropertyPath(() => obj.Field));
		exception.Message.ShouldContain("member", Case.Insensitive);

		// Static property
		exception = Should.Throw<ArgumentException>(() => JsonUtilities.GetPropertyPath((SerializableTestType obj) => SerializableTestType.StaticProperty));
		exception.Message.ShouldContain("member", Case.Insensitive);

		// Constant expression
		exception = Should.Throw<ArgumentException>(() => JsonUtilities.GetPropertyPath((SerializableTestType obj) => 1));
		exception.Message.ShouldContain("member", Case.Insensitive);
		exception = Should.Throw<ArgumentException>(() => JsonUtilities.GetPropertyPath(() => 1));
		exception.Message.ShouldContain("member", Case.Insensitive);

		// Parameter expression
		exception = Should.Throw<ArgumentException>(() => JsonUtilities.GetPropertyPath((SerializableTestType obj) => obj));
		exception.Message.ShouldContain("member", Case.Insensitive);
	}

	[Fact]
	public void GetPropertyPath_WithoutJsonPropertyAttribute_ShouldThrow()
	{
		var exception = Should.Throw<ArgumentException>(() => JsonUtilities.GetPropertyPath((SerializableTestType obj) => obj.Property));
		exception.Message.ShouldContain("Attribute", Case.Insensitive);
	}

	[Fact]
	public void GetPropertyPath_WithSimpleProperty_ShouldYieldExpectedResult()
	{
		var obj = new SerializableTestType();

		var result1 = JsonUtilities.GetPropertyPath((SerializableTestType obj) => obj.JsonProperty);
		var result2 = JsonUtilities.GetPropertyPath(() => obj.JsonProperty);

		result1.ShouldBe("/SeriTest_JsonProp");
		result2.ShouldBe(result1);
	}

	[Fact]
	public void GetPropertyPath_WithNestedProperty_ShouldYieldExpectedResult()
	{
		var obj = new SerializableTestType();

		var result1 = JsonUtilities.GetPropertyPath((SerializableTestType obj) => obj.Nested!.JsonProperty);
		var result2 = JsonUtilities.GetPropertyPath(() => obj.Nested!.JsonProperty);

		result1.ShouldBe("/SeriTest_Nested/SeriTest_JsonProp");
		result2.ShouldBe(result1);
	}
}
