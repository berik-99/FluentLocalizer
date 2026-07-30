namespace FluentLocalizer.Core.Logging;

/// <summary>
/// Represents the supported severity levels for translation logging.
/// </summary>
public enum TranslationLogLevel
{
    /// <summary>Most verbose level for tracing internal execution.</summary>
    Trace,
    /// <summary>Used for detailed diagnostics during translation resolution.</summary>
    Debug,
    /// <summary>Used for informational events.</summary>
    Information,
    /// <summary>Used for recoverable issues such as fallback values.</summary>
    Warning,
    /// <summary>Used for failures that stop normal resolution.</summary>
    Error,
    /// <summary>Used for unrecoverable failures.</summary>
    Critical
}