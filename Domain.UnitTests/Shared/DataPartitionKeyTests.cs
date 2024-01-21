namespace Rtl.News.RtlPoc.Domain.UnitTests.Shared;

public sealed class DataPartitionKeyTests
{
	[Theory]
	[InlineData(null, "")]
	[InlineData("", "")]
	[InlineData("a", "a")]
	[InlineData("0", "0")]
	[InlineData(".", ".")]
	[InlineData("[]{}<>", "[]{}<>")]
	[InlineData("_-", "_-")]
	[InlineData("|One|two|123", "|One|two|123")]
	[InlineData("💩💩", "💩💩")] // Emoji
	[InlineData("漢字", "漢字")] // Kanji
	[InlineData("'€”", "'€”")] // Single quote, euro sign, closing double quote
	[InlineData("   ", "   ")] // Space, en space, em space
	[InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890", "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890")]
	public void CreateForArbitraryString_WithValidInput_ShouldProduceExpectedResult(string? input, string expectedResult)
	{
		var result = DataPartitionKey.CreateForArbitraryString(input!);

		result.Value.ShouldBe(expectedResult);
	}

	[Theory]
	[InlineData("12345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901")]
	public void CreateForArbitraryString_WithOversizedInput_ShouldThrow(string input)
	{
		var exception = Should.Throw<ValidationException>(() => DataPartitionKey.CreateForArbitraryString(input));
		exception.ErrorCode.ShouldBe("PartitionKey_ValueTooLong");
	}

	[Theory]
	[InlineData("?")]
	[InlineData("#")]
	[InlineData("/")]
	[InlineData(@"\")]
	[InlineData(@"""")]
	[InlineData("\r")]
	[InlineData("\n")]
	[InlineData("\t")]
	[InlineData("\u2028")] // Unicode line separator
	[InlineData("\u2029")] // Unicode paragraph separator
	[InlineData("\u0094")] // Cancel character (control char)
	[InlineData("\uE000")] // Character in unicode's "private use" space
	[InlineData("FineFineFineOrNot\r")]
	[InlineData("Hidden💩\t💩SomewhereInTheMiddle")]
	public void CreateForArbitraryString_WithUnsupportedChar_ShouldThrow(string input)
	{
		var exception = Should.Throw<ValidationException>(() => DataPartitionKey.CreateForArbitraryString(input));
		exception.ErrorCode.ShouldBe("PartitionKey_ValueInvalid");
	}

	[Fact]
	public void CreateRandom_Regularly_ShouldYieldRandom3CharResult()
	{
		var result1 = DataPartitionKey.CreateRandom();
		var result2 = DataPartitionKey.CreateRandom();

		result1.Value.Length.ShouldBe(3);
		result2.Value.Length.ShouldBe(3);

		result1.Value.Distinct().Count().ShouldBeGreaterThanOrEqualTo(2);
		result2.Value.Distinct().Count().ShouldBeGreaterThanOrEqualTo(2);

		result2.Value.ShouldNotBe(result1.Value);
	}

	/// <summary>
	/// We use the last few chars of a UUID as the partition key.
	/// </summary>
	[Theory]
	[InlineData("aaaaaaaaaaaaaaaaaaaaaa", "aaa")]
	[InlineData("0000000000000000000000", "000")]
	[InlineData("48XoooHHCe1CiOHrghM7Dl", "7Dl")]
	[InlineData(@"#?\/""€€€€€€€€€€€€€€7Dl", "7Dl")]
	[InlineData("Hidden💩💩InTheMiddle.", "le.")]
	public void CastFromString_WithSuitableInput_ShouldProduceExpectedResult(string? input, string expectedResult)
	{
		var result = (DataPartitionKey)input!;

		result.Value.ShouldBe(expectedResult);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("1")]
	[InlineData("1234567890123456789012345678901")]
	[InlineData("123456789012345678901234567890123")]
	public void CastFromString_WithAnythingOtherThanAnAlphanumericUuid_ShouldThrow(string? input)
	{
		var exception = Should.Throw<InvalidOperationException>(() => (DataPartitionKey)input!);
		exception.Message.ShouldContain("22");
		exception.Message.ShouldContain("UUID");
	}

	[Theory]
	[InlineData("######################")]
	[InlineData("??????????????????????")]
	[InlineData("//////////////////////")]
	[InlineData(@"\\\\\\\\\\\\\\\\\\\\\\")]
	[InlineData(@"""""""""""""""""""""""""""""""""""""""""""""")]
	[InlineData("FineFineFineOrNot....\r")]
	[InlineData("FineFineFineOrNot....\n")]
	[InlineData("FineFineFineOrNot....\t")]
	[InlineData("AAAAAAAAAAAAAAAAAAAAA\u2028")] // Unicode line separator
	[InlineData("AAAAAAAAAAAAAAAAAAAAA\u2029")] // Unicode paragraph separator
	[InlineData("AAAAAAAAAAAAAAAAAAAAA\u0094")] // Cancel character (control char)
	[InlineData("AAAAAAAAAAAAAAAAAAAAA\uE000")] // Character in unicode's "private use" space
	public void CastFromString_WithCorrectLengthButUnsupportedChar_ShouldThrow(string input)
	{
		var exception = Should.Throw<ValidationException>(() => (DataPartitionKey)input);
		exception.ErrorCode.ShouldBe("PartitionKey_ValueInvalid");
	}

	[Theory]
	[InlineData("abc", "1234567890123456789abc")]
	[InlineData("000", "1234567890123456789000")]
	[InlineData("9xY", "12345678901234567899xY")]
	public void MatchesId_WithUuidWithMatchingLast3Chars_ShouldReturnTrue(string partitionKeyValue, string id)
	{
		var partitionKey = DataPartitionKey.CreateForArbitraryString(partitionKeyValue);

		var result = partitionKey.MatchesId(id);

		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData("abc", "1234567890123456789aBc")]
	[InlineData("000", "1234567890123456789001")]
	[InlineData("9xY", "12345678901234567899xy")]
	public void MatchesId_WithUuidWithMismatchingLast3Chars_ShouldReturnFalse(string partitionKeyValue, string id)
	{
		var partitionKey = DataPartitionKey.CreateForArbitraryString(partitionKeyValue);

		var result = partitionKey.MatchesId(id);

		result.ShouldBeFalse();
	}

	[Theory]
	[InlineData("abcdefg", "abcdefg")]
	[InlineData("Je Moeder", "Je Moeder")]
	[InlineData("Jan&Piet", "Jan&Piet")]
	public void MatchesId_WithArbitraryIdenticalId_ShouldReturnTrue(string partitionKeyValue, string id)
	{
		var partitionKey = DataPartitionKey.CreateForArbitraryString(partitionKeyValue);

		var result = partitionKey.MatchesId(id);

		result.ShouldBeTrue();
	}

	[Theory]
	[InlineData("abcdefg", "abcdefgg")]
	[InlineData("abcdefg", "aabcdefg")]
	[InlineData("Je Moeder", "Je MoedeR")]
	[InlineData("Jan&Piet", "Jan_Piet")]
	public void MatchesId_WithArbitraryMismatchingId_ShouldReturnFalse(string partitionKeyValue, string id)
	{
		var partitionKey = DataPartitionKey.CreateForArbitraryString(partitionKeyValue);

		var result = partitionKey.MatchesId(id);

		result.ShouldBeFalse();
	}
}
