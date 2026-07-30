#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FluentLocalizer.Core;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Defines the contract for resolving translations by key.
/// </summary>
public interface ITranslator
{
    /// <summary>
    /// Creates a builder for the specified translation key.
    /// </summary>
    /// <param name="key">The translation key to resolve.</param>
    /// <returns>A builder that can be configured and resolved.</returns>
    TranslationBuilder Get(string key);
}
