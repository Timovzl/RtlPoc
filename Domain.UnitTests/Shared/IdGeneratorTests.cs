using System.Text.Unicode;

namespace Rtl.News.RtlPoc.Domain.UnitTests.Shared;

public sealed class IdGeneratorTests
{
	[Fact]
	public void CreateId_WithSpan_ShouldMatchPrimaryOverload()
	{
		using var idGeneratorScope = new DistributedId128GeneratorScope(new CustomDistributedId128Generator(1234567));

		var stringResult = IdGenerator.CreateId();

		Span<byte> spanResult = stackalloc byte[22];
		IdGenerator.CreateId(spanResult);

		Span<byte> utf8StringResult = stackalloc byte[22];
		Utf8.FromUtf16(stringResult, utf8StringResult, out _, out _);

		spanResult.ToArray().ShouldBe(utf8StringResult.ToArray());
	}

	[Fact]
	public void CreateId_Regularly_ShouldYieldExpectedResult()
	{
		var result = IdGenerator.CreateId();

		result.Length.ShouldBe(22);
		result.ShouldAllBe(chr => Char.IsAsciiLetterOrDigit(chr));
		result.Distinct().Count().ShouldBeGreaterThanOrEqualTo(8);
	}

	[Fact]
	public void CreateId_WithCustomGeneratorScope_ShouldYieldExpectedResult()
	{
		using var idGeneratorScope = new DistributedId128GeneratorScope(new IncrementalDistributedId128Generator());

		var result1 = IdGenerator.CreateId();
		var result2 = IdGenerator.CreateId();
		var result3 = IdGenerator.CreateId();

		result1.ShouldBe("0000000000000000000001");
		result2.ShouldBe("0000000000000000000002");
		result3.ShouldBe("0000000000000000000003");
	}

	[Fact]
	public void CreateIdGeneratorScopeForSinglePartition_FollowedByCreateId_ShouldYieldExpectedResult()
	{
		var partitionKey = DataPartitionKey.CreateForArbitraryString("abc");
		using var idGeneratorScope = IdGenerator.CreateIdGeneratorScopeForSinglePartition(partitionKey);

		var result1 = IdGenerator.CreateId();
		var result2 = IdGenerator.CreateId();
		var result3 = IdGenerator.CreateId();

		result1[^3..].ShouldBe("abc");
		result2[^3..].ShouldBe("abc");

		result2[..^3].ShouldNotBe(result1[..^3]);

		result1[..^3].ShouldAllBe(chr => Char.IsAsciiLetterOrDigit(chr));
		result1[..^3].Distinct().Count().ShouldBeGreaterThanOrEqualTo(8);

		result2[..^3].ShouldAllBe(chr => Char.IsAsciiLetterOrDigit(chr));
		result2[..^3].Distinct().Count().ShouldBeGreaterThanOrEqualTo(8);
	}

	[Fact]
	public void CreateIdGeneratorScopeForSinglePartition_WithoutPartitionKey_ShouldUseRandomKey()
	{
		using var idGeneratorScope1 = IdGenerator.CreateIdGeneratorScopeForSinglePartition();

		var result1 = IdGenerator.CreateId()[^3..];

		using var idGeneratorScope2 = IdGenerator.CreateIdGeneratorScopeForSinglePartition();

		var result2 = IdGenerator.CreateId()[^3..];

		result1.Distinct().Count().ShouldBeGreaterThanOrEqualTo(2);
		result2.Distinct().Count().ShouldBeGreaterThanOrEqualTo(2);
		result2.ShouldNotBe(result1);
	}

	/// <summary>
	/// Creating a new UUID in the same partition as another only makes sense if that partition was obtained from a UUID.
	/// </summary>
	[Theory]
	[InlineData("")]
	[InlineData("1")]
	[InlineData("12")]
	[InlineData("1234")]
	[InlineData("1234567890123456789012")]
	public void CreateIdInPartition_WithUnsuitablePartitionKey_ShouldThrow(string input)
	{
		var partitionKey = DataPartitionKey.CreateForArbitraryString(input);

		var exception = Should.Throw<InvalidOperationException>(() => IdGenerator.CreateIdInPartition(partitionKey));
		exception.Message.ShouldContain("22");
		exception.Message.ShouldContain("UUID");
	}

	[Theory]
	[InlineData("0000000000000000000000")]
	[InlineData("48XoooHHCe1CiOHrghM7Dl")]
	public void CreateIdInPartition_WithSuitablePartitionKey_ShouldProduceUniqueIdInThatPartition(string input)
	{
		var partitionKey = (DataPartitionKey)input;

		var results = new List<string>();
		for (var i = 0; i < 1000; i++)
		{
			var result = IdGenerator.CreateIdInPartition(partitionKey);

			results.Add(result);

			result.Length.ShouldBe(input.Length); // Same length as input ID
			result.ShouldAllBe(chr => Char.IsAsciiLetterOrDigit(chr)); // Alphanumeric
			result.ShouldNotBe(input); // Different value from input ID
			result[^3..].ShouldBe(input[^3..]); // Same ending as input ID
			((DataPartitionKey)result).Value.ShouldBe(partitionKey); // Same partition
		}

		results.Distinct().Count().ShouldBe(results.Count); // All unique
	}
}
