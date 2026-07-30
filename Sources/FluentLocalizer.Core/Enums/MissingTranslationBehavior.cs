#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FluentLocalizer.Core;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines the behavior used when a required translation key cannot be found.
/// </summary>
public enum MissingTranslationBehavior
{
    /// <summary>
    /// Returns a placeholder value for missing translations.
    /// </summary>
    ReturnPlaceholder,
    /// <summary>
    /// Throws an exception when a translation key is missing.
    /// </summary>
    ThrowException,
    /// <summary>
    /// Returns the configured fallback value for missing translations.
    /// </summary>
    ReturnConfiguredValue
}
