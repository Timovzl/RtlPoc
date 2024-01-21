using Microsoft.Extensions.Logging;
using NSubstitute.ReceivedExtensions;
using Rtl.News.RtlPoc.Application.Promises;
using Rtl.News.RtlPoc.Application.Storage;
using Shouldly;

namespace Rtl.News.RtlPoc.Application.IntegrationTests.Promises;

public sealed class PromiseFulfillerTests : IntegrationTestBase
{
	private sealed class TestUseCase
	{
		public bool ShouldThrow { get; set; }
		public int InvocationCount { get; set; }

		[IdempotentPromiseFulfiller("PromiseFulfillerTests_FulfillAsync")]
		public Task FulfillAsync(Promise _, CancellationToken cancellationToken)
		{
			this.InvocationCount++;

			if (this.ShouldThrow)
				throw new InvalidDataException("Test exception.");

			cancellationToken.ThrowIfCancellationRequested();

			return Task.CompletedTask;
		}
	}

	private PromiseFulfiller Instance => this.Host.Services.GetRequiredService<PromiseFulfiller>();

	private ILogger<PromiseFulfiller> Logger { get; }

	private TestUseCase UseCase => this.Host.Services.GetRequiredService<TestUseCase>();

	public PromiseFulfillerTests()
	{
		this.Logger = Substitute.For<ILogger<PromiseFulfiller>>();

		this.ConfigureServices(services => services.AddSingleton<TestUseCase>());
		this.ConfigureServices(services => services.AddSingleton(this.Logger));
	}

	[Fact]
	public async Task TryFulfillAsync_WithException_ShouldSucceedAndLogWarning()
	{
		// Arrange

		var promise = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Hello");
		promise.Delay(TimeSpan.Zero);

		await using var transactionForAdding = await this.Repository.CreateTransactionAsync(promise.PartitionKey, CancellationToken.None);
		await transactionForAdding
			.AddAsync(promise)
			.CommitAsync();

		promise.SuppressImmediateFulfillment();
		await transactionForAdding.DisposeAsync();

		promise = await this.Repository.LoadAsync<Promise>(query => query.Where(x => x.Id == promise.Id), CancellationToken.None, new ReadOptions() { FullyConsistent = true });

		promise.ShouldNotBeNull();

		promise.ClaimForAttempt();

		await using var transactionForUpdating = await this.Repository.CreateTransactionAsync(promise.PartitionKey, CancellationToken.None);
		await transactionForUpdating
			.UpdateAsync(promise)
			.CommitAsync();

		this.UseCase.ShouldThrow = true;

		// Act

		await this.Instance.TryFulfillAsync(promise, CancellationToken.None);

		// Assert

		this.Logger.Received(1).Log(LogLevel.Warning, message =>
			message.Contains("PromiseFulfillerTests_FulfillAsync") &&
			message.Contains("Test exception."));

		this.Logger.Received(0).Log(LogLevel.Error);
	}

	[Fact]
	public async Task TryFulfillAsync_WithCancelation_ShouldTerminateSilently()
	{
		// Arrange

		var promise = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Hello");
		promise.Delay(TimeSpan.Zero);

		await using var transactionForAdding = await this.Repository.CreateTransactionAsync(promise.PartitionKey, CancellationToken.None);
		await transactionForAdding
			.AddAsync(promise)
			.CommitAsync();

		promise.SuppressImmediateFulfillment();
		await transactionForAdding.DisposeAsync();

		promise = await this.Repository.LoadAsync<Promise>(query => query.Where(x => x.Id == promise.Id), CancellationToken.None, new ReadOptions() { FullyConsistent = true });

		promise.ShouldNotBeNull();

		promise.ClaimForAttempt();

		await using var transactionForUpdating = await this.Repository.CreateTransactionAsync(promise.PartitionKey, CancellationToken.None);
		await transactionForUpdating
			.UpdateAsync(promise)
			.CommitAsync();

		// Act

		await this.Instance.TryFulfillAsync(promise, new CancellationToken(canceled: true));

		// Assert

		this.Logger.Received(0).Log(LogLevel.Information);
		this.Logger.Received(0).Log(LogLevel.Warning);
		this.Logger.Received(0).Log(LogLevel.Error);
	}

	[Fact]
	public async Task TryFulfillAsync_WithNewlyCreatedAndStoredPromise_ShouldCallRequestedMethodAndDeletePromise()
	{
		// Arrange

		var promise = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Hello");

		await using var transaction = await this.Repository.CreateTransactionAsync(promise.PartitionKey, CancellationToken.None);
		await transaction
			.AddAsync(promise)
			.CommitAsync();

		promise.IsFirstAttempt.ShouldBeTrue();

		// Act

		await this.Instance.TryFulfillAsync(promise, CancellationToken.None);

		// Assert

		this.UseCase.InvocationCount.ShouldBe(1);

		this.Logger.Received(0).Log(LogLevel.Warning);
		this.Logger.Received(0).Log(LogLevel.Error);
	}

	[Fact]
	public async Task TryFulfillAsync_WithPromiseThatWasLoadedAndClaimedAndUpdatedInStorage_ShouldCallRequestedMethodAndDeletePromise()
	{
		// Arrange

		var promise = Promise.Create((TestUseCase useCase) => useCase.FulfillAsync, data: "Hello");
		promise.Delay(TimeSpan.Zero);

		await using var transactionForAdding = await this.Repository.CreateTransactionAsync(promise.PartitionKey, CancellationToken.None);
		await transactionForAdding
			.AddAsync(promise)
			.CommitAsync();

		promise.SuppressImmediateFulfillment();
		await transactionForAdding.DisposeAsync();

		promise = await this.Repository.LoadAsync<Promise>(query => query.Where(x => x.Id == promise.Id), CancellationToken.None, new ReadOptions() { FullyConsistent = true });

		promise.ShouldNotBeNull();

		promise.ClaimForAttempt();

		await using var transactionForUpdating = await this.Repository.CreateTransactionAsync(promise.PartitionKey, CancellationToken.None);
		await transactionForUpdating
			.UpdateAsync(promise)
			.CommitAsync();

		promise.IsFirstAttempt.ShouldBeFalse();

		// Act

		await this.Instance.TryFulfillAsync(promise, CancellationToken.None);

		// Assert

		this.UseCase.InvocationCount.ShouldBe(1);

		this.Logger.Received(0).Log(LogLevel.Warning);
		this.Logger.Received(0).Log(LogLevel.Error);

		var remainingPromises = await this.Repository.ListAsync<Promise>(query => query.Where(x => x.Due >= default(DateTimeOffset)), CancellationToken.None, new MultiReadOptions() { FullyConsistent = true });
		remainingPromises.ShouldBeEmpty();
	}
}
