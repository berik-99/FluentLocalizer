using FluentLocalizer.Core.Logging;

namespace FluentLocalizer.Core;

/// <summary>
/// Resolves translation templates by creating a fluent builder for a specific key.
/// </summary>
/// <param name="store">The translation store used to retrieve templates.</param>
/// <param name="options">The options controlling missing-key and formatting behavior.</param>
/// <param name="logger">An optional logger used to report translation events.</param>
public class Translator(ITranslationStore store, TranslationOptions? options = null, ITranslationLogger? logger = null) : ITranslator
{
    private readonly ITranslationStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly TranslationOptions _options = options ?? new TranslationOptions();
    private readonly ITranslationLogger? _logger = logger;

    /// <summary>
    /// Creates a builder that can resolve a translation template for the specified key.
    /// </summary>
    /// <param name="key">The translation key to resolve.</param>
    /// <returns>A builder that can be configured with culture, arguments, and formatting options.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
    public TranslationBuilder Get(string key) => new(_store, key, _options, null, _logger);
}
