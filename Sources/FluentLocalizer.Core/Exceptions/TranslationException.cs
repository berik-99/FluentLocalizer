#pragma warning disable RCS1194 // Implement exception constructors
using System.Globalization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FluentLocalizer.Core;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents an error raised when a translation cannot be resolved or formatted successfully.
/// </summary>
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

#pragma warning restore RCS1194 // Implement exception constructors