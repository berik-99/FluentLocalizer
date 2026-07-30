#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FluentLocalizer.Core;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines the behavior used when a translation template cannot be formatted.
/// </summary>
public enum FormattingErrorBehavior
{
    /// <summary>
    /// Returns a placeholder error value when formatting fails.
    /// </summary>
    ReturnPlaceholder,
    /// <summary>
    /// Throws an exception when formatting fails.
    /// </summary>
    ThrowException
}