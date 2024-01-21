namespace Rtl.News.RtlPoc.Application.Shared;

/// <summary>
/// <para>
/// Used to execute the <typeparamref name="TUseCase"/> from within the container's <see cref="IResilienceStrategy"/>.
/// </para>
/// <para>
/// For example, the registered <see cref="IResilienceStrategy"/> may automatically retry the entire operation upon losing an optimistic concurrency conflict.
/// </para>
/// <para>
/// For use cases that return a result, use <see cref="ResilientService{TUseCase, TResult}"/> instead.
/// </para>
/// </summary>
/// <param name="resilienceStrategy">Injects the general <see cref="IResilienceStrategy"/> for performing application-level code in a resilient way.</param>
/// <param name="useCase">Injects the <see cref="IUseCase"/> to expose.</param>
public sealed class ResilientService<TUseCase>(
	IResilienceStrategy resilienceStrategy,
	TUseCase useCase)
	where TUseCase : UseCase // For use cases with a return value, instead use the more generic class that also takes a result type
{
	public Task ExecuteAsync(CancellationToken cancellationToken)
	{
		return resilienceStrategy.ExecuteAsync(
			operation: useCase.ExecuteAsync,
			cancellationToken: cancellationToken);
	}
}

/// <summary>
/// <para>
/// Used to execute the <typeparamref name="TUseCase"/> from within the container's <see cref="IResilienceStrategy"/> and return its resulting <typeparamref name="TResult"/>.
/// </para>
/// <para>
/// For example, the registered <see cref="IResilienceStrategy"/> may automatically retry the entire operation upon losing an optimistic concurrency conflict.
/// </para>
/// </summary>
/// <param name="resilienceStrategy">Injects the general <see cref="IResilienceStrategy"/> for performing application-level code in an resilient way.</param>
/// <param name="useCase">Injects the <see cref="UseCase{TResult}"/> to expose.</param>
public sealed class ResilientService<TUseCase, TResult>(
	IResilienceStrategy resilienceStrategy,
	TUseCase useCase)
	where TUseCase : UseCase<TResult>
{
	public Task<TResult> ExecuteAsync(CancellationToken cancellationToken)
	{
		return resilienceStrategy.ExecuteAsync(
			operation: useCase.ExecuteAsync,
			cancellationToken: cancellationToken);
	}
}
