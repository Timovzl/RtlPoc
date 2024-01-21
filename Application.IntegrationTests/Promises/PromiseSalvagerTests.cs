using Microsoft.Extensions.Logging;
using NSubstitute.ReceivedExtensions;
using Rtl.News.RtlPoc.Application.Promises;
using Rtl.News.RtlPoc.Application.Storage;
using Rtl.News.RtlPoc.Infrastructure.Databases.Promises;
using Rtl.News.RtlPoc.Infrastructure.Databases.Shared;
using Shouldly;

namespace Rtl.News.RtlPoc.Application.IntegrationTests.Promises;

public sealed class PromiseSalvagerTests : IntegrationTestBase
{
	private sealed class TestUseCase
	{
		public int InvocationCount { get; set; }

		[IdempotentPromiseFulfiller("PromiseSalvagerTests_FulfillAsync")]
		public Task FulfillAsync(Promise _, CancellationToken cancellationToken)
		{
			this.InvocationCount++;

			cancellationToken.ThrowIfCancellationRequested();

			return Task.CompletedTask;
		}
	}

	private PromiseSalvager Instance => (PromiseSalvager)this.Host.Services.GetRequiredService<IPromiseSalvager>();

	private ILogger<CosmosPromiseSalvager> Logger { get; }

	private TestUseCase UseCase => this.Host.Services.GetRequiredService<TestUseCase>();

	public PromiseSalvagerTests()
	{
		this.Logger = Substitute.For<ILogger<CosmosPromiseSalvager>>();

		this.ConfigureServices(services => services.AddSingleton<TestUseCase>());
		this.ConfigureServices(services => services.AddSingleton(this.Logger));
	}

	[Fact]
	public async Task TryFulfillDuePromisesAsync_WithException_ShouldSucceedAndLogError()
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

		// Cause an exception
		var databaseClient = this.Host.Services.GetRequiredService<DatabaseClient>();
		var container = databaseClient.Container;
		databaseClient.GetType().GetProperty(nameof(DatabaseClient.Container))!.SetValue(databaseClient, null!);

		// Act
		try
		{
			await this.Instance.TryFulfillDuePromisesAsync(CancellationToken.None);
		}
		finally
		{
			databaseClient.GetType().GetProperty(nameof(DatabaseClient.Container))!.SetValue(databaseClient, container);
		}

		// Assert

		this.Logger.Received(1).Log(LogLevel.Error, message => message.Contains("Background fulfillment of neglected promises encountered an error"));

		this.UseCase.InvocationCount.ShouldBe(0);
	}

	[Fact]
	public async Task TryFulfillDuePromisesAsync_WithCancelation_ShouldTerminateSilently()
	{
		// Arrange

		// Act

		await this.Instance.TryFulfillDuePromisesAsync(new CancellationToken(canceled: true));

		this.Logger.Received(0).Log(LogLevel.Information);
		this.Logger.Received(0).Log(LogLevel.Warning);
		this.Logger.Received(0).Log(LogLevel.Error);
	}

	[Fact]
	public async Task TryFulfillDuePromisesAsync_WithPromisesThatWereLoadedAndClaimedAndUpdatedInStorage_ShouldCallRequestedMethodsAndDeletePromises()
	{
		// Arrange

		using var idGeneratorScope = IdGenerator.CreateIdGeneratorScopeForSinglePartition();

		var promises = Enumerable.Range(0, 11)
			.Select(_ => Promise.Create((TestUseCase useCase) => useCase.FulfillAsync))
			.ToList();

		foreach (var promise in promises)
			promise.Delay(TimeSpan.Zero);

		await using var transactionForAdding = await this.Repository.CreateTransactionAsync(promises[0].PartitionKey, CancellationToken.None);
		await transactionForAdding
			.AddRangeAsync(promises)
			.CommitAsync();

		foreach (var promise in promises)
			promise.SuppressImmediateFulfillment();

		await transactionForAdding.DisposeAsync();

		// Act

		await this.Instance.TryFulfillDuePromisesAsync(CancellationToken.None);

		// Assert

		this.UseCase.InvocationCount.ShouldBe(11);

		this.Logger.Received(0).Log(LogLevel.Warning);
		this.Logger.Received(0).Log(LogLevel.Error);

		var remainingPromises = await this.Repository.ListAsync<Promise>(query => query.Where(x => x.Due >= default(DateTimeOffset)), CancellationToken.None, new MultiReadOptions() { FullyConsistent = true });
		remainingPromises.ShouldBeEmpty();
	}
}
