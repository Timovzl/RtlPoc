namespace Rtl.News.RtlPoc.Application.Shared;

/// <summary>
/// <para>
/// A strategy for executing application-level code in a resilient way.
/// </para>
/// <para>
/// For example, a strategy may automatically retry the entire operation upon losing an optimistic concurrency conflict.
/// </para>
/// </summary>
public interface IResilienceStrategy
{
    Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);
    Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken);
}

/// <summary>
/// An <see cref="IResilienceStrategy"/> that simply executes a given operation once.
/// </summary>
public sealed class BasicResilienceStrategy : IResilienceStrategy
{
    public BasicResilienceStrategy(ILogger<BasicResilienceStrategy> logger)
    {
        logger.LogWarning("A basic execution strategy was used, which is generally only intended for automated testing");
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        return operation(cancellationToken);
    }

    public Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken)
    {
        return operation(cancellationToken);
    }
}
