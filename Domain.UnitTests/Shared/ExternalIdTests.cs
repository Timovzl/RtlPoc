namespace Rtl.News.RtlPoc.Domain.UnitTests.Shared;

public sealed class ExternalIdTests
{
    [Fact]
    public void Construct_WithNullValue_ShouldThrow()
    {
        var exception = Should.Throw<NullValidationException>(() => new ExternalId(null!));
        exception.ErrorCode.ShouldBe("ExternalId_ValueNull");
    }

    [Fact]
    public void Construct_WithEmptyValue_ShouldThrow()
    {
        var exception = Should.Throw<ValidationException>(() => new ExternalId(""));
        exception.ErrorCode.ShouldBe("ExternalId_ValueEmpty");
    }

    [Theory]
    [InlineData("123456789012345678901234567890123456789012345678901")] // 51 chars
    [InlineData("💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩💩")] // 52 (UTF-16) chars
    public void Construct_WithOversizedValue_ShouldThrow(string value)
    {
        var exception = Should.Throw<ValidationException>(() => new ExternalId(value));
        exception.ErrorCode.ShouldBe("ExternalId_ValueTooLong");
    }

    [Theory]
    [InlineData("A ")]
    [InlineData("A	")]
    [InlineData("A\n")]
    [InlineData("A'")]
    [InlineData(@"A""")]
    [InlineData("A💩")]
    [InlineData("Hidden💩InTheMiddle")]
    [InlineData("Hidden InTheMiddle")]
    public void Construct_WithInvalidChars_ShouldThrow(string value)
    {
        var exception = Should.Throw<ValidationException>(() => new ExternalId(value));
        exception.ErrorCode.ShouldBe("ExternalId_ValueInvalid");
    }

    [Theory]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1088824355131185736905670087")]
    [InlineData("48XoooHHCe1CiOHrghM7Dl")]
    [InlineData("!@#&()-[{}]:;,?/*`~$^+=<>")] // All printable ASCII chars except quotes and alphanumerics
    public void Construct_WithValidValue_ShouldYieldExpectedResult(string value)
    {
        var result = new ExternalId(value);

        result.Value.ShouldBe(value);
    }
}
