using System.Globalization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FluentLocalizer.Core;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines the storage contract used by the translator to retrieve templates.
/// </summary>
public interface ITranslationStore
{
    /// <summary>
    /// Asynchronously returns a template for the specified key and culture.
    /// </summary>
    /// <param name="key">The translation key to retrieve.</param>
    /// <param name="culture">The culture used to select a localized template.</param>
    /// <param name="cancellationToken">A token that can cancel the asynchronous operation.</param>
    /// <returns>The matching template, or <c>null</c> when no template exists.</returns>
    Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously returns a template for the specified key and culture.
    /// </summary>
    /// <param name="key">The translation key to retrieve.</param>
    /// <param name="culture">The culture used to select a localized template.</param>
    /// <returns>The matching template, or <c>null</c> when no template exists.</returns>
    string? GetTemplate(string key, CultureInfo culture);
}