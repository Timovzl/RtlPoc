using Newtonsoft.Json;
using Rtl.News.RtlPoc.Application.Promises;
using Shouldly;

namespace Rtl.News.RtlPoc.Application.UnitTests.Promises;

public sealed class PromiseTests
{
	private sealed class TestUseCase
	{
		[IdempotentPromiseFulfiller("PromiseTests_FulfillAsync")]
		public Task FulfillAsync(Promise _, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.CompletedTask;
		}
	}

	private static Promise GetExamplePromiseLoadedFromStorage()
	{
		const string JsonBlob = """
			{
				"id": "0000000000000000000001",
				"part": "001",
				"_etag": "Test",
				"_ts": 1,
				"Promise_Due": "1970-01-01T00:00:00.0000000+00:00",
				"Promise_AtpCnt": 1,
				"Promise_Act": "PromiseTests_FulfillAsync",
				"Promise_Dta": "Test"
			}
			""";

		var result = JsonConvert.DeserializeObject<Promise>(JsonBlob)!;
		return result;
	}

	[Fact]
	public void ClaimDuration_Always_ShouldYieldExpectedResult()
	{
		var result = Promise.ClaimDuration;

		result.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void Create_WithPromisedAction_ShouldYieldExpectedResult()
	{
		using var clockScope = new ClockScope(DateTime.UnixEpoch);
		using var idGeneratorScope = new DistributedId128GeneratorScope(new CustomDistributedId128Generator(1));

		var result = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Test\n💩/\\");

		result.ToString().ShouldBe("{Promise 'PromiseTests_FulfillAsync' (0000000000000000000001)}");
		result.Id.ShouldBe("0000000000000000000001");
		result.PartitionKey.Value.ShouldBe("001");
		result.ETag.ShouldBeNull();
		result.Due.ShouldBe(DateTime.UnixEpoch.Add(Promise.ClaimDuration));
		result.AttemptCount.ShouldBe((ushort)1);
		result.ActionName.ShouldBe("PromiseTests_FulfillAsync");
		result.Data.ShouldBe("Test\n💩/\\");
		result.AvailableAttemptCount.ShouldBe(0);
		result.HasTimeToFulfill.ShouldBeTrue();
	}

	[Fact]
	public void SetETag_Regularly_ShouldProvideOneAvailableAttempt()
	{
		var instance = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Test\n💩/\\");

		instance.AvailableAttemptCount.ShouldBe(0);

		instance.ETag = "Test";

		instance.AvailableAttemptCount.ShouldBe(1);
		instance.IsFirstAttempt.ShouldBeTrue();
	}

	[Fact]
	public void Delay_IrrespectiveOfExistingDelay_ShouldSetGivenDelay()
	{
		using var arrangementClockScope = new ClockScope(DateTime.UnixEpoch);

		var instance = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Test\n💩/\\");

		using var actionClockScope = new ClockScope(DateTime.UnixEpoch.AddHours(1));

		instance.Delay();

		instance.Due.ShouldBe(DateTime.UnixEpoch.AddHours(1).Add(Promise.ClaimDuration));

		instance.Delay(TimeSpan.FromSeconds(1));

		instance.Due.ShouldBe(DateTime.UnixEpoch.AddHours(1).AddSeconds(1));
	}

	[Fact]
	public void ClaimForAttempt_WhenNotLoadedFromStorage_ShouldThrow()
	{
		var instance = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Test\n💩/\\");

		var exception = Should.Throw<InvalidOperationException>(instance.ClaimForAttempt);
		exception.Message.ShouldContain("storage");
	}

	[Fact]
	public void ClaimForAttempt_WhenNotDue_ShouldThrow()
	{
		var instance = GetExamplePromiseLoadedFromStorage();

		using var clockScope = new ClockScope(DateTime.MinValue.ToUniversalTime());

		var exception = Should.Throw<InvalidOperationException>(instance.ClaimForAttempt);
		exception.Message.ShouldContain("due");
	}

	[Fact]
	public void ClaimForAttempt_WhenJustLoadedFromStorage_ShouldDelayAndIncrementAttemptCount()
	{
		var instance = GetExamplePromiseLoadedFromStorage();

		using var clockScope = new ClockScope(DateTime.UnixEpoch.AddDays(3000 * 365));

		instance.Due.ShouldBeLessThanOrEqualTo(Clock.UtcNow);

		instance.ClaimForAttempt();

		instance.AttemptCount.ShouldBe((ushort)2);
		instance.Due.ShouldBe(DateTime.UnixEpoch.AddDays(3000 * 365).Add(Promise.ClaimDuration));
	}

	[Fact]
	public void ConsumeAttempt_WhenNotStored_ShouldThrow()
	{
		var instance = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Test\n💩/\\");

		instance.ETag.ShouldBeNull();

		var exception = Should.Throw<InvalidOperationException>(instance.ConsumeAttempt);
		exception.Message.ShouldContain("storage");
	}

	[Fact]
	public void ConsumeAttempt_AfterSuppressingImmediateFulfillment_ShouldThrow()
	{
		var instance = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Test\n💩/\\");

		instance.ETag = "Test";
		instance.SuppressImmediateFulfillment();

		instance.AvailableAttemptCount.ShouldBe(0);

		var exception = Should.Throw<InvalidOperationException>(instance.ConsumeAttempt);
		exception.Message.ShouldContain("claim");
	}

	[Fact]
	public void ConsumeAttempt_WithoutClaim_ShouldThrow()
	{
		var instance = GetExamplePromiseLoadedFromStorage();

		instance.AvailableAttemptCount.ShouldBe(0);

		var exception = Should.Throw<InvalidOperationException>(instance.ConsumeAttempt);
		exception.Message.ShouldContain("claim");
	}

	[Fact]
	public void ConsumeAttempt_WithUnsavedClaim_ShouldThrow()
	{
		var instance = GetExamplePromiseLoadedFromStorage();

		// Claim, but do not update in storage
		instance.ClaimForAttempt();

		instance.AvailableAttemptCount.ShouldBe(0);

		var exception = Should.Throw<InvalidOperationException>(instance.ConsumeAttempt);
		exception.Message.ShouldContain("claim");
	}

	[Fact]
	public void ConsumeAttempt_WithoutSufficientTimeBeforeClaimExpires_ShouldThrow()
	{
		var instance = GetExamplePromiseLoadedFromStorage();

		using var arrangementClockScope = new ClockScope(DateTime.UnixEpoch);

		// Claim
		instance.ClaimForAttempt();

		// Simulate updating in storage
		instance.ETag = "TestNext";

		// Advance the clock through more than half of the claim duration
		using var actionClockScope = new ClockScope(DateTime.UnixEpoch.Add(Promise.ClaimDuration * 0.51));

		instance.HasTimeToFulfill.ShouldBeFalse();

		var exception = Should.Throw<InvalidOperationException>(instance.ConsumeAttempt);
		exception.Message.ShouldContain("up-to-date");
	}

	[Fact]
	public void ConsumeAttempt_WhenJustLoadedAndClaimedAndUpdatedInStorage_ShouldYieldExpectedResult()
	{
		var instance = GetExamplePromiseLoadedFromStorage();

		using var arrangementClockScope = new ClockScope(DateTime.UnixEpoch);

		// Claim
		instance.ClaimForAttempt();

		// Simulate updating in storage
		instance.ETag = "TestNext";

		instance.ETag.ShouldNotBeNull();
		instance.AvailableAttemptCount.ShouldBe(1);
		instance.HasTimeToFulfill.ShouldBeTrue();
		instance.IsFirstAttempt.ShouldBeFalse();

		instance.ConsumeAttempt();

		instance.AvailableAttemptCount.ShouldBe(0);
	}

	[Fact]
	public void ConsumeAttempt_WhenJustCreatedAndAddedToStorage_ShouldSucceedAndDecrementAvailableAttemptCount()
	{
		var instance = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Test\n💩/\\");

		// Simulate adding to storage
		instance.ETag = "Test";

		instance.ETag.ShouldNotBeNull();
		instance.AvailableAttemptCount.ShouldBe(1);
		instance.HasTimeToFulfill.ShouldBeTrue();
		instance.IsFirstAttempt.ShouldBeTrue();

		instance.ConsumeAttempt();

		instance.AvailableAttemptCount.ShouldBe(0);
	}
}
