namespace Rtl.News.RtlPoc.Domain.UnitTests.Shared;

public sealed class EnumerableExtensionsTests
{
	public enum TestEnum : short
	{
		One = 1,
		Max = Int16.MaxValue,
		Min = Int16.MinValue,
		Two = 2,
	}

	[Theory]
	[InlineData(TestEnum.One, TestEnum.Min, "")] // Inverted range, i.e. nothing
	[InlineData(TestEnum.One, TestEnum.One, "One")]
	[InlineData(TestEnum.One, TestEnum.Two, "One,Two")]
	[InlineData(TestEnum.Min, TestEnum.Max, "Min,One,Two,Max")]
	[InlineData(TestEnum.Min, TestEnum.Two, "Min,One,Two")]
	[InlineData(TestEnum.One, TestEnum.Max, "One,Two,Max")]
	public void Through_Regularly_ShouldYieldExpectedResult(TestEnum from, TestEnum through, string expectedResultString)
	{
		var result = from.Through(through);

		var expectedResult = expectedResultString.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<TestEnum>);
		result.ShouldBe(expectedResult);
	}

	[Theory]
	[InlineData(TestEnum.One, TestEnum.Min, "")] // Inverted range, i.e. nothing
	[InlineData(TestEnum.One, TestEnum.One, "")]
	[InlineData(TestEnum.One, TestEnum.Two, "One")]
	[InlineData(TestEnum.Min, TestEnum.Max, "Min,One,Two")]
	[InlineData(TestEnum.Min, TestEnum.Two, "Min,One")]
	[InlineData(TestEnum.One, TestEnum.Max, "One,Two")]
	public void Until_Regularly_ShouldYieldExpectedResult(TestEnum from, TestEnum through, string expectedResultString)
	{
		var result = from.Until(through);

		var expectedResult = expectedResultString.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<TestEnum>);
		result.ShouldBe(expectedResult);
	}
}
