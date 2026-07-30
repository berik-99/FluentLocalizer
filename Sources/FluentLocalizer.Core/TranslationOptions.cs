using FluentLocalizer.Core.Polyfill;
using System.Globalization;

namespace FluentLocalizer.Core;

/// <summary>
/// Configures how FluentLocalizer handles missing keys, formatting errors, and default arguments.
/// </summary>
public sealed class TranslationOptions
{
    /// <summary>
    /// Gets or sets the behavior used when a translation key is missing in the store.
    /// </summary>
    public MissingTranslationBehavior MissingKeyBehavior { get; init; } = MissingTranslationBehavior.ReturnPlaceholder;

    /// <summary>
    /// Gets or sets the fallback text returned when missing-key resolution is configured to use a value.
    /// </summary>
    public string MissingKeyFallbackValue { get; init; } = "[{key}]";

    /// <summary>
    /// Gets or sets a callback used to create a custom exception for missing translations.
    /// </summary>
    public Func<string, CultureInfo?, Exception>? MissingKeyExceptionFactory { get; init; }

    /// <summary>
    /// Gets or sets the behavior used when a translation template cannot be formatted successfully.
    /// </summary>
    public FormattingErrorBehavior FormattingErrorBehavior { get; init; } = FormattingErrorBehavior.ReturnPlaceholder;

    /// <summary>
    /// Gets or sets the fallback text returned when formatting errors are configured to use a value.
    /// </summary>
    public string FormattingErrorFallbackValue { get; init; } = "[Format Error]";

    /// <summary>
    /// Gets or sets a callback used to create a custom exception for formatting failures.
    /// </summary>
    public Func<string, CultureInfo?, Exception>? FormattingErrorExceptionFactory { get; init; }

    /// <summary>
    /// Gets or sets the default arguments applied to every translation request unless overridden at runtime.
    /// </summary>
    public IReadOnlyDictionary<string, object?>? DefaultArguments { get; init; }

    /// <summary>
    /// Merges default arguments and runtime arguments into a single argument set.
    /// </summary>
    /// <param name="runtimeArguments">The arguments supplied by the current builder invocation.</param>
    /// <returns>A dictionary containing the merged argument values.</returns>
    public IReadOnlyDictionary<string, object?> CreateArgumentSet(IReadOnlyDictionary<string, object?>? runtimeArguments)
    {
        Dictionary<string, object?> mergedArguments = new(StringComparer.OrdinalIgnoreCase);

        if (DefaultArguments is not null)
        {
            foreach (var argument in DefaultArguments)
            {
                mergedArguments[argument.Key] = argument.Value;
            }
        }

        if (runtimeArguments is not null)
        {
            foreach (var argument in runtimeArguments)
            {
                mergedArguments[argument.Key] = argument.Value;
            }
        }

        return mergedArguments;
    }

    /// <summary>
    /// Creates an exception that describes a missing translation key.
    /// </summary>
    /// <param name="key">The translation key that could not be resolved.</param>
    /// <param name="culture">The culture associated with the missing translation.</param>
    /// <returns>An exception describing the missing translation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    public Exception CreateMissingKeyException(string key, CultureInfo? culture)
    {
        Throw.IfNullOrWhiteSpace(key);

        if (MissingKeyExceptionFactory is not null)
        {
            return MissingKeyExceptionFactory(key, culture);
        }

        string cultureName = culture?.Name ?? "unknown";
        return new TranslationException(
            key,
            culture,
            $"Unable to resolve translation key '{key}' for culture '{cultureName}'. Configure the missing key behavior or provide a translation template.");
    }

    /// <summary>
    /// Creates an exception that describes a formatting failure.
    /// </summary>
    /// <param name="key">The translation key that failed to format.</param>
    /// <param name="culture">The culture associated with the formatting operation.</param>
    /// <param name="innerException">The original formatting exception, if any.</param>
    /// <returns>An exception describing the formatting failure.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null, empty, or whitespace.</exception>
    public Exception CreateFormattingException(string key, CultureInfo? culture, Exception? innerException)
    {
        Throw.IfNullOrWhiteSpace(key);

        if (FormattingErrorExceptionFactory is not null)
        {
            return FormattingErrorExceptionFactory(key, culture) ?? CreateMissingKeyException(key, culture);
        }

        string detail = innerException?.Message is { Length: > 0 } message ? $" Details: {message}" : string.Empty;
        string cultureName = culture?.Name ?? "unknown";
        return new TranslationException(
            key,
            culture,
            $"Unable to format translation '{key}' for culture '{cultureName}'.{detail}");
    }
}
