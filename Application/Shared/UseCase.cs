using Architect.DomainModeling;

namespace Rtl.News.RtlPoc.Application.Shared;

/// <summary>
/// <para>
/// Represents an application service exposing a single use case.
/// </para>
/// <para>
/// Inherit from <see cref="UseCase"/> or <see cref="UseCase{TResult}"/> rather than implementing this directly.
/// </para>
/// </summary>
public interface IUseCase : IApplicationService
{
}

/// <summary>
/// Represents an application service exposing a single use case with no return value.
/// </summary>
public abstract class UseCase : IUseCase
{
	public abstract Task ExecuteAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Represents an application service exposing a single use case that asynchronously returns a result of type <typeparamref name="TResult"/>.
/// </summary>
public abstract class UseCase<TResult> : IUseCase
{
	public abstract Task<TResult> ExecuteAsync(CancellationToken cancellationToken);
}
