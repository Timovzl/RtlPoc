using Prometheus;

namespace Rtl.News.RtlPoc.Application.Promises;

/// <summary>
/// A service with the ability to fulfill a promise.
/// </summary>
public interface IPromiseFulfiller
{
	/// <summary>
	/// <para>
	/// Attempts to fulfill the given <see cref="Promise"/> by <strong>executing</strong> its defined action and then <strong>deleting</strong> it from storage.
	/// </para>
	/// <para>
	/// With the exception of any <see cref="InvalidOperationException"/> caused by incorrect use, exceptions do not bubble up from this method.
	/// They are logged as warnings (or eventually as errors), since the <see cref="IPromiseSalvager"/> will fulfill the promise eventually.
	/// </para>
	/// <para>
	/// Because no storage exceptions bubble up from this method, it can safely be run from an outer <see cref="IResilienceStrategy"/> without causing retries of already-committed outer work.
	/// </para>
	/// </summary>
	Task TryFulfillAsync(Promise promise, CancellationToken cancellationToken);
}

public sealed class PromiseFulfiller(
	ILogger<PromiseFulfiller> logger,
	IResilienceStrategy resilienceStrategy,
	IRepository repository,
	IServiceProvider serviceProvider)
	: IPromiseFulfiller
{
	private static readonly Counter FulfillmentCounter = Metrics.CreateCounter(name: "PromiseFulfillerSuccesses", help: "Counts successfully fulfilled promises.");
	private static readonly Counter DelayedFulfillmentCounter = Metrics.CreateCounter(name: "PromiseFulfillerDelayedSuccesses", help: "Counts promises that were fulfilled delayed.");
	private static readonly Counter ErrorCounter = Metrics.CreateCounter(name: "PromiseFulfillerErrors", help: "Counts errors attempting to fulfill promises.");

	public async Task TryFulfillAsync(Promise promise, CancellationToken cancellationToken)
	{
		promise.ConsumeAttempt();

		var currentStep = "fulfill";

		try
		{
			await resilienceStrategy.ExecuteAsync(
				cancellationToken => IdempotentPromiseFulfillerAttribute.ExecuteActionAsync(serviceProvider, promise, cancellationToken),
				cancellationToken);

			currentStep = "delete";

			await resilienceStrategy.ExecuteAsync(
				cancellationToken => DeleteAsync(repository, CancellationToken.None), // Without cancellation, to best keep in-sync with having been executed
				cancellationToken);

			FulfillmentCounter.Inc();
			if (promise.AttemptCount > 1)
				DelayedFulfillmentCounter.Inc();
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			// Stop cleanly
			// The next attempt will come in time
		}
		catch (Exception e)
		{
			ErrorCounter.Inc();

			// Warn about the issue
			// The next attempt will come in time
			logger.Log(
				promise.AttemptCount > 20 ? LogLevel.Error : LogLevel.Warning,
				"Failed to {Step} promise '{ActionName}' ({Id}) during attempt {AttemptNumber}: {ExceptionType}: {ExceptionMessage}",
				currentStep, promise.ActionName, promise.Id, promise.AttemptCount, e.GetType().Name, e.Message);
		}

		// Local function that deletes the current promise
		async Task DeleteAsync(IRepository repository, CancellationToken cancellationToken)
		{
			await using var transaction = await repository.CreateTransactionAsync((DataPartitionKey)promise.Id, cancellationToken);
			await transaction
				.DeleteAsync(promise, new ModificationOptions() { IgnoresConcurrencyProtection = true, }) // No matter what
				.CommitAsync();
		}
	}
}
