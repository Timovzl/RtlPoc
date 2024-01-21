using Microsoft.Extensions.Logging;

namespace Rtl.News.RtlPoc.Testing.Common;

/// <summary>
/// Allows the non-generic <see cref="ILogger"/> to be used in place of <see cref="ILogger{TCategoryName}"/> where <typeparamref name="T"/> has <em>internal</em> accessibility (unusable to the mocking framework).
/// </summary>
public sealed class LoggerWrapperForInternalType<T>(
	ILogger logger)
	: ILogger<T>
{
	public IDisposable? BeginScope<TState>(TState state)
		where TState : notnull
	{
		return logger.BeginScope(state);
	}

	public bool IsEnabled(LogLevel logLevel)
	{
		return logger.IsEnabled(logLevel);
	}

	public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
	{
		logger.Log(logLevel, eventId, state, exception, formatter);
	}
}
