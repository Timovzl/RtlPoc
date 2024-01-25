using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Rtl.News.RtlPoc.Testing.Common;

/// <summary>
/// Provides extensions to <see cref="ILogger"/> for testing purposes.
/// </summary>
public static class LoggerExtensions
{
    /// <summary>
    /// Delegates to the non-extension log method, passing <see cref="Arg.Any{T}"/> for all unspecified parameters, but confirming that the message meets the given <see cref="Predicate{T}"/>.
    /// </summary>
    public static void Log(this ILogger logger, Predicate<string> messagePredicate)
    {
        logger.Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Is<Arg.AnyType>((object obj) => messagePredicate(obj.ToString()!)),
            Arg.Any<Exception>(), Arg.Any<Func<Arg.AnyType, Exception?, string>>());
    }

    /// <summary>
    /// Delegates to the non-extension log method, passing <see cref="Arg.Any{T}"/> for all unspecified parameters, but confirming that the message meets the given <see cref="Predicate{T}"/>.
    /// </summary>
    public static void Log(this ILogger logger, LogLevel logLevel, Predicate<string> messagePredicate)
    {
        logger.Log(
            logLevel,
            Arg.Any<EventId>(),
            Arg.Is<Arg.AnyType>((object obj) => messagePredicate(obj.ToString()!)),
            Arg.Any<Exception>(), Arg.Any<Func<Arg.AnyType, Exception?, string>>());
    }

    /// <summary>
    /// Delegates to the non-extension log method, passing <see cref="Arg.Any{T}"/> for all unspecified parameters.
    /// </summary>
    public static void Log(this ILogger logger, LogLevel logLevel)
    {
        logger.Log(
            logLevel,
            Arg.Any<EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Any<Exception>(), Arg.Any<Func<Arg.AnyType, Exception?, string>>());
    }
}
