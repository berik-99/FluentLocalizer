namespace FluentLocalizer.Core.Logging;

/// <summary>
/// Defines the contract for logging translation resolution events.
/// </summary>
public interface ITranslationLogger
{
    /// <summary>
    /// Writes a translation-related log entry.
    /// </summary>
    /// <param name="level">The severity level of the log entry.</param>
    /// <param name="message">The human-readable message to log.</param>
    /// <param name="exception">An optional exception associated with the log entry.</param>
    public void Log(TranslationLogLevel level, string message, Exception? exception = null);
}