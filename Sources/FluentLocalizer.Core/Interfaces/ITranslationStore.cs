using System.Globalization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FluentLocalizer;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines the storage contract used by the translator to retrieve templates.
/// </summary>
/// <remarks>Implementations may apply their own culture fallback rules. Return <see langword="null"/> when no template is available; a missing key may also be reported by throwing <see cref="KeyNotFoundException"/>, which the translator treats as a missing translation.</remarks>
public interface ITranslationStore
{
    /// <summary>
    /// Asynchronously returns a template for the specified key and culture.
    /// </summary>
    /// <param name="key">The translation key to retrieve.</param>
    /// <param name="culture">The culture used to select a localized template.</param>
    /// <param name="cancellationToken">A token that can cancel the asynchronous operation.</param>
    /// <returns>The matching template, or <c>null</c> when no template exists.</returns>
    /// <exception cref="OperationCanceledException">The operation was canceled through <paramref name="cancellationToken"/>.</exception>
    Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronously returns a template for the specified key and culture.
    /// </summary>
    /// <param name="key">The translation key to retrieve.</param>
    /// <param name="culture">The culture used to select a localized template.</param>
    /// <returns>The matching template, or <c>null</c> when no template exists.</returns>
    /// <remarks>This method must return without performing asynchronous work. Use <see cref="GetTemplateAsync"/> for stores that require asynchronous I/O.</remarks>
    string? GetTemplate(string key, CultureInfo culture);
}
