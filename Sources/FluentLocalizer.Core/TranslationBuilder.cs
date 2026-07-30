using FluentLocalizer.Core.Logging;
using Jeffijoe.MessageFormat;
using System.Globalization;
using System.Text;

namespace FluentLocalizer.Core;

/// <summary>
/// Builds and resolves a translation request by combining store access, culture, arguments, and formatting options.
/// </summary>
public class TranslationBuilder(ITranslationStore store, string key, TranslationOptions? options = null, CultureInfo? culture = null, ITranslationLogger? logger = null)
{
    private const string genderDefaultKey = "gender";
    private const string quantityDefaultKey = "quantity";

    private readonly ITranslationStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly string _key = key ?? throw new ArgumentNullException(nameof(key));
    private readonly ITranslationLogger? _logger = logger;
    private TranslationOptions _options = options ?? new TranslationOptions();
    private CultureInfo _culture = culture ?? CultureInfo.CurrentUICulture;
    private readonly Dictionary<string, object?> _arguments =
        new(StringComparer.OrdinalIgnoreCase) { { genderDefaultKey, nameof(Gender.Unspecified).ToLowerInvariant() }, { quantityDefaultKey, null } };
    private LetterCase _case = LetterCase.AsIs;

    #region Fluent API
    /// <summary>
    /// Sets the culture used to resolve the translation template.
    /// </summary>
    /// <param name="culture">The culture to use. When null, the current UI culture is applied.</param>
    /// <returns>The current builder instance to allow fluent composition.</returns>
    public TranslationBuilder WithCulture(CultureInfo culture)
    {
        _culture = culture ?? CultureInfo.CurrentCulture;
        return this;
    }

    /// <summary>
    /// Sets the culture used to resolve the translation template from a culture name.
    /// </summary>
    /// <param name="cultureName">The culture name to resolve, such as <c>en-US</c>.</param>
    /// <returns>The current builder instance to allow fluent composition.</returns>
    /// <exception cref="CultureNotFoundException">Thrown when the supplied culture name is not recognized.</exception>
    public TranslationBuilder WithCulture(string cultureName)
    {
        _culture = CultureInfo.GetCultureInfo(cultureName);
        return this;
    }

    /// <summary>
    /// Replaces the builder options with a new configuration object.
    /// </summary>
    /// <param name="options">The configuration used for missing key and formatting behaviors.</param>
    /// <returns>The current builder instance to allow fluent composition.</returns>
    public TranslationBuilder WithOptions(TranslationOptions options)
    {
        _options = options ?? new TranslationOptions();
        return this;
    }

    /// <summary>
    /// Supplies the quantity used for pluralization in message formatting.
    /// </summary>
    /// <param name="quantity">The quantity value to expose as the <c>quantity</c> argument.</param>
    /// <returns>The current builder instance to allow fluent composition.</returns>
    public TranslationBuilder Pluralize(int quantity) => WithArg(quantityDefaultKey, quantity);

    /// <summary>
    /// Supplies the gender used for gender-aware message formatting.
    /// </summary>
    /// <param name="gender">The gender value to expose as the <c>gender</c> argument.</param>
    /// <returns>The current builder instance to allow fluent composition.</returns>
    public TranslationBuilder Genderize(Gender gender) => WithArg(genderDefaultKey, gender.ToString().ToLowerInvariant());

    /// <summary>
    /// Adds or replaces a named runtime argument for the template.
    /// </summary>
    /// <param name="name">The argument name.</param>
    /// <param name="value">The argument value to pass to the formatter.</param>
    /// <returns>The current builder instance to allow fluent composition.</returns>
    public TranslationBuilder WithArg(string name, object? value)
    {
        _arguments[name] = value;
        return this;
    }

    /// <summary>
    /// Adds a set of runtime arguments to the current builder.
    /// </summary>
    /// <param name="args">A dictionary of argument names and values to merge into the current request.</param>
    /// <returns>The current builder instance to allow fluent composition.</returns>
    public TranslationBuilder WithArgs(Dictionary<string, object?> args)
    {
        if (args != null)
        {
            foreach (var arg in args)
            {
                WithArg(arg.Key, arg.Value);
            }
        }
        return this;
    }

    /// <summary>
    /// Applies a text transformation to the formatted message.
    /// </summary>
    /// <param name="letterCase">The casing strategy to apply after formatting.</param>
    /// <returns>The current builder instance to allow fluent composition.</returns>
    public TranslationBuilder WithCase(LetterCase letterCase)
    {
        _case = letterCase;
        return this;
    }

    /// <summary>
    /// Resolves the translation template asynchronously.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the asynchronous store operation.</param>
    /// <returns>The resolved message or a fallback value based on the configured behavior.</returns>
    /// <exception cref="TranslationException">Thrown when the configured behavior is to throw for missing keys or formatting errors.</exception>
    public async Task<string> ResolveAsync(CancellationToken cancellationToken = default)
    {
        var culture = _culture ?? CultureInfo.CurrentUICulture;
        LogDebug($"Resolving translation request for key '{_key}' for culture '{culture.Name}'.");

        try
        {
            var template = await _store.GetTemplateAsync(_key, culture, cancellationToken).ConfigureAwait(false);
            LogDebug($"Retrieved translation template for key '{_key}' for culture '{culture.Name}'.");
            return ResolveTemplate(template);
        }
        catch (KeyNotFoundException)
        {
            return HandleMissingTemplate();
        }
        catch (Exception ex) when (ex is not TranslationException)
        {
            LogError($"An unexpected exception occurred while resolving translation key '{_key}'.", ex);
            throw;
        }
    }

    /// <summary>
    /// Resolves the translation template synchronously.
    /// </summary>
    /// <returns>The resolved message or a fallback value based on the configured behavior.</returns>
    /// <exception cref="TranslationException">Thrown when the configured behavior is to throw for missing keys or formatting errors.</exception>
    public string Resolve()
    {
        var culture = _culture ?? CultureInfo.CurrentUICulture;
        LogDebug($"Resolving translation request for key '{_key}' for culture '{culture.Name}'.");

        try
        {
            return ResolveTemplate(_store.GetTemplate(_key, culture));
        }
        catch (KeyNotFoundException)
        {
            return HandleMissingTemplate();
        }
        catch (Exception ex) when (ex is not TranslationException)
        {
            LogError($"An unexpected exception occurred while resolving translation key '{_key}'.", ex);
            throw;
        }
    }

    #endregion Fluent API

    /// <summary>
    /// Formats a raw template with the current arguments and applies the configured case transformation.
    /// </summary>
    /// <param name="rawTemplate">The template content retrieved from the store.</param>
    /// <returns>The formatted message, or a fallback value if the template is missing or malformed.</returns>
    private string ResolveTemplate(string? rawTemplate)
    {
        LogDebug($"Formatting translation template for key '{_key}' for culture '{_culture?.Name ?? "unknown"}'.");

        if (string.IsNullOrWhiteSpace(rawTemplate))
        {
            return HandleMissingTemplate();
        }

        try
        {
            MessageFormatter mf = new();
            var argumentSet = _options.CreateArgumentSet(_arguments);
            var template = rawTemplate ?? string.Empty;

            if (template.Contains("{Length}") && !argumentSet.ContainsKey("Length"))
            {
                Dictionary<string, object?> lengthArguments = argumentSet.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.OrdinalIgnoreCase
                );
                lengthArguments["Length"] = argumentSet.Values.Count(static value => value is not null);
                argumentSet = lengthArguments;
            }

            string formatted = mf.FormatMessage(template, argumentSet, _culture);
            var result = ApplyCaseTransformation(formatted, _case);
            LogDebug($"Translation request completed for key '{_key}' for culture '{_culture?.Name ?? "unknown"}'.");
            return result;
        }
        catch (Exception ex) when (ex is not TranslationException)
        {
            return HandleFormattingException(ex);
        }
    }

    /// <summary>
    /// Handles missing templates according to the configured missing key policy.
    /// </summary>
    /// <returns>The fallback string returned for the missing template.</returns>
    private string HandleMissingTemplate()
    {
        string message = $"Translation key '{_key}' could not be resolved for culture '{_culture?.Name ?? "unknown"}'.";

        return _options.MissingKeyBehavior switch
        {
            MissingTranslationBehavior.ThrowException => ThrowMissingTemplate(message),
            MissingTranslationBehavior.ReturnConfiguredValue => ReturnMissingTemplateFallback(message, _options.MissingKeyFallbackValue),
            _ => ReturnMissingTemplateFallback(message, $"[{_key}]")
        };
    }

    /// <summary>
    /// Handles formatting exceptions according to the configured formatting error policy.
    /// </summary>
    /// <param name="exception">The exception raised during message formatting.</param>
    /// <returns>The fallback string returned for the formatting failure.</returns>
    private string HandleFormattingException(Exception exception)
    {
        string message = $"Formatting failed for translation key '{_key}' for culture '{_culture?.Name ?? "unknown"}'.";

        return _options.FormattingErrorBehavior switch
        {
            FormattingErrorBehavior.ThrowException => ThrowFormattingException(message, exception),
            _ => ReturnFormattingFallback(message, _options.FormattingErrorFallbackValue)
        };
    }

    /// <summary>
    /// Throws the configured missing-key exception after logging the failure.
    /// </summary>
    /// <param name="message">The log message describing the missing translation.</param>
    /// <returns>Never returns; always throws.</returns>
    private string ThrowMissingTemplate(string message)
    {
        var missingException = _options.CreateMissingKeyException(_key, _culture);
        LogError(message, missingException);
        throw missingException;
    }

    private string ReturnMissingTemplateFallback(string message, string fallbackValue)
    {
        var resolvedValue = ReplaceFallbackTokens(fallbackValue);
        LogWarning($"{message} Returning fallback value '{resolvedValue}'.");
        return resolvedValue;
    }

    private string ThrowFormattingException(string message, Exception exception)
    {
        var formattingException = _options.CreateFormattingException(_key, _culture, exception);
        LogError(message, formattingException);
        throw formattingException;
    }

    private string ReturnFormattingFallback(string message, string fallbackValue)
    {
        var resolvedValue = ReplaceFallbackTokens(fallbackValue);
        LogWarning($"{message} Returning fallback value '{resolvedValue}'.");
        return resolvedValue;
    }

    private void LogDebug(string message) => _logger?.Log(TranslationLogLevel.Debug, message);

    private void LogWarning(string message, Exception? exception = null) => _logger?.Log(TranslationLogLevel.Warning, message, exception);

    private void LogError(string message, Exception? exception = null) => _logger?.Log(TranslationLogLevel.Error, message, exception);

    /// <summary>
    /// Replaces the placeholder tokens supported by the fallback values.
    /// </summary>
    /// <param name="fallbackValue">The fallback string that may contain token values.</param>
    /// <returns>A fallback string with the translation key and culture tokens resolved.</returns>
    private string ReplaceFallbackTokens(string fallbackValue) => fallbackValue
            .Replace("{key}", _key, StringComparison.OrdinalIgnoreCase)
            .Replace("{culture}", _culture?.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{0}", _key, StringComparison.OrdinalIgnoreCase)
            .Replace("{1}", _culture?.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Converts the formatted text to a string representation of the builder for implicit string conversion.
    /// </summary>
    /// <param name="builder">The builder instance to resolve.</param>
    /// <returns>The resolved string value of the builder.</returns>
    public static implicit operator string(TranslationBuilder builder) => builder.Resolve();

    /// <summary>
    /// Returns the resolved translation as a string.
    /// </summary>
    /// <returns>The resolved translation message.</returns>
    public override string ToString() => Resolve();

    /// <summary>
    /// Applies the requested case transformation to the formatted text.
    /// </summary>
    /// <param name="text">The formatted text to transform.</param>
    /// <param name="letterCase">The requested casing strategy.</param>
    /// <returns>The transformed text, or the original string when the transformation is not applicable.</returns>
    private static string ApplyCaseTransformation(string text, LetterCase letterCase)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return letterCase switch
        {
            LetterCase.Upper => text.ToUpperInvariant(),
            LetterCase.Lower => text.ToLowerInvariant(),
            LetterCase.CamelCase => ToCamelCase(text),
            LetterCase.PascalCase => ToPascalCase(text),
            LetterCase.SnakeCase => ToJoinedCase(text, '_'),
            LetterCase.KebabCase => ToJoinedCase(text, '-'),
            _ => text
        };
    }

    /// <summary>
    /// Converts text to camel case by splitting it into words and normalizing each piece.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>The camel-cased representation of the input text.</returns>
    private static string ToCamelCase(string text)
    {
        var words = SplitWords(text);
        if (words.Count == 0)
        {
            return text;
        }

        StringBuilder builder = new();
        for (int index = 0; index < words.Count; index++)
        {
            var word = words[index];
            if (string.IsNullOrWhiteSpace(word))
            {
                continue;
            }

            if (index == 0)
            {
                builder.Append(char.ToLowerInvariant(word[0]));
                if (word.Length > 1)
                {
                    builder.Append(word[1..].ToLowerInvariant());
                }
            }
            else
            {
                builder.Append(char.ToUpperInvariant(word[0]));
                if (word.Length > 1)
                {
                    builder.Append(word[1..].ToLowerInvariant());
                }
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Converts text to PascalCase by splitting it into words and capitalizing each piece.
    /// </summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>The PascalCase representation of the input text.</returns>
    private static string ToPascalCase(string text)
    {
        var words = SplitWords(text);
        if (words.Count == 0)
        {
            return text;
        }

        StringBuilder builder = new();
        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(word[0]));
            if (word.Length > 1)
            {
                builder.Append(word[1..].ToLowerInvariant());
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Joins the words of the input text using a separator and lowercases each word.
    /// </summary>
    /// <param name="text">The text to split and join.</param>
    /// <param name="separator">The separator used between words.</param>
    /// <returns>The joined representation of the text.</returns>
    private static string ToJoinedCase(string text, char separator)
    {
        var words = SplitWords(text);
        if (words.Count == 0)
        {
            return text;
        }

        return string.Join(separator.ToString(), words.Select(word => word.ToLowerInvariant()));
    }

    /// <summary>
    /// Splits a text value into a list of words based on letter, digit, and casing boundaries.
    /// </summary>
    /// <param name="text">The text to analyze.</param>
    /// <returns>A list of words representing the meaningful chunks of the input.</returns>
    private static List<string> SplitWords(string text)
    {
        List<string> words = [];
        StringBuilder current = new();

        for (int index = 0; index < text.Length; index++)
        {
            char currentChar = text[index];
            if (char.IsLetterOrDigit(currentChar))
            {
                if (current.Length > 0)
                {
                    char previousChar = text[index - 1];
                    char? nextChar = index + 1 < text.Length ? text[index + 1] : null;
                    if (IsWordBoundary(previousChar, currentChar, nextChar))
                    {
                        AddWord(words, current);
                        current.Clear();
                    }
                }

                current.Append(currentChar);
            }
            else
            {
                AddWord(words, current);
                current.Clear();
            }
        }

        AddWord(words, current);
        return words;
    }

    /// <summary>
    /// Determines whether the transition between two characters should be treated as a word boundary.
    /// </summary>
    /// <param name="previousChar">The previous character in the text.</param>
    /// <param name="currentChar">The current character being evaluated.</param>
    /// <param name="nextChar">The next character, if any.</param>
    /// <returns><c>true</c> when the characters indicate a new word should start.</returns>
    private static bool IsWordBoundary(char previousChar, char currentChar, char? nextChar)
    {
        if (!char.IsLetterOrDigit(previousChar) || !char.IsLetterOrDigit(currentChar))
        {
            return false;
        }

        if (char.IsLower(previousChar) && char.IsUpper(currentChar))
        {
            return true;
        }

        if (char.IsUpper(previousChar) && char.IsUpper(currentChar) && nextChar.HasValue && char.IsLower(nextChar.Value))
        {
            return true;
        }

        if (char.IsDigit(previousChar) && char.IsLetter(currentChar))
        {
            return true;
        }

        if (char.IsLetter(previousChar) && char.IsDigit(currentChar))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Adds a completed word to the collection when a token boundary is detected.
    /// </summary>
    /// <param name="words">The words accumulated so far.</param>
    /// <param name="current">The current character buffer for the word being assembled.</param>
    private static void AddWord(List<string> words, StringBuilder current)
    {
        if (current.Length > 0)
        {
            words.Add(current.ToString());
            current.Clear();
        }
    }
}
