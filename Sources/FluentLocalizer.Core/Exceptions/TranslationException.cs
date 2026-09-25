using System.Globalization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FluentLocalizer;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents an error raised when a translation cannot be resolved or formatted successfully.
/// </summary>
/// <param name="key">The translation key associated with the failure.</param>
/// <param name="culture">The culture used for the translation request, if known.</param>
/// <param name="message">A message that describes the failure.</param>
/// <param name="innerException">The underlying exception that caused this failure, if any.</param>
public class TranslationException(string key, CultureInfo? culture, string message, Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Gets the translation key associated with the exception.
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Gets the culture associated with the exception, if any.
    /// </summary>
    public CultureInfo? Culture { get; } = culture;
}
