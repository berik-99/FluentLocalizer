#pragma warning disable CA2254 // Template should be a static expression
using FluentLocalizer.Core.Logging;
using Microsoft.Extensions.Logging;

namespace FluentLocalizer.Core;

internal sealed class ILoggerAdapter(ILogger<Translator> logger) : ITranslationLogger
{
    public void Log(TranslationLogLevel level, string message, Exception? exception = null)
    {
        var logLevel = Convert(level);
        if (logger.IsEnabled(logLevel))
            logger.Log(logLevel, exception, message);
    }

    private static LogLevel Convert(TranslationLogLevel level) => level switch
    {
        TranslationLogLevel.Trace => LogLevel.Trace,
        TranslationLogLevel.Debug => LogLevel.Debug,
        TranslationLogLevel.Information => LogLevel.Information,
        TranslationLogLevel.Warning => LogLevel.Warning,
        TranslationLogLevel.Error => LogLevel.Error,
        TranslationLogLevel.Critical => LogLevel.Critical,
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
    };
}
#pragma warning restore CA2254 // Template should be a static expression