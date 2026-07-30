using FluentLocalizer.Core;
using System.Globalization;
using System.Reflection;

namespace FluentLocalizer.Test;

public class TranslationBuilderTests
{
    [Fact]
    public void Resolve_returns_formatted_value_from_store()
    {
        InMemoryTranslationStore store = new();
        store.AddTemplate("welcome", "Hello {name}!");
        Translator translator = new(store);

        var result = translator.Get("welcome")
            .WithArg("name", "Ada")
            .Resolve();

        Assert.Equal("Hello Ada!", result);
    }

    [Fact]
    public async Task ResolveAsync_returns_formatted_value_from_store()
    {
        InMemoryTranslationStore store = new();
        store.AddTemplate("welcome", "Hello {name}!");
        Translator translator = new(store);

        var result = await translator.Get("welcome")
            .WithArg("name", "Grace")
            .ResolveAsync(CancellationToken.None);

        Assert.Equal("Hello Grace!", result);
    }

    [Fact]
    public void Resolve_throws_translation_exception_when_missing_key_behavior_is_throw()
    {
        InMemoryTranslationStore store = new();
        Translator translator = new(
            store,
            new TranslationOptions
            {
                MissingKeyBehavior = MissingTranslationBehavior.ThrowException
            });

        var exception = Assert.Throws<TranslationException>(() => translator.Get("missing").Resolve());

        Assert.Equal("missing", exception.Key);
        Assert.Equal(CultureInfo.CurrentUICulture, exception.Culture);
    }

    [Fact]
    public void Resolve_uses_configured_value_for_missing_templates()
    {
        InMemoryTranslationStore store = new();
        Translator translator = new(
            store,
            new TranslationOptions
            {
                MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
                MissingKeyFallbackValue = "Missing '{key}' for culture '{culture}'."
            });

        var result = translator.Get("missing")
            .WithCulture("it-IT")
            .Resolve();

        Assert.Equal("Missing 'missing' for culture 'it-IT'.", result);
    }

    [Fact]
    public void Resolve_uses_default_arguments_from_translation_options_when_missing()
    {
        InMemoryTranslationStore store = new();
        store.AddTemplate("welcome", "Hello {name}!");
        Translator translator = new(
            store,
            new TranslationOptions
            {
                DefaultArguments = new Dictionary<string, object?>
                {
                    ["name"] = "Ada"
                }
            });

        var result = translator.Get("welcome")
            .Resolve();

        Assert.Equal("Hello Ada!", result);
    }

    [Fact]
    public void Resolve_returns_generic_error_when_formatting_placeholder_is_missing()
    {
        InMemoryTranslationStore store = new();
        store.AddTemplate("welcome", "Hello {name}!");
        Translator translator = new(store);

        var result = translator.Get("welcome")
            .Resolve();

        Assert.Equal("[Format Error]", result);
    }

    [Fact]
    public void Resolve_throws_translation_exception_with_format_details_when_configured()
    {
        InMemoryTranslationStore store = new();
        store.AddTemplate("welcome", "Hello {name}!");
        Translator translator = new(
            store,
            new TranslationOptions
            {
                FormattingErrorBehavior = FormattingErrorBehavior.ThrowException,
                FormattingErrorExceptionFactory = (key, culture) => new TranslationException(
                    key,
                    culture,
                    $"Custom format exception for {key} in {culture?.Name ?? "unknown"}")
            });

        var exception = Assert.Throws<TranslationException>(() => translator.Get("welcome")
            .Resolve());

        Assert.Equal("Custom format exception for welcome in en-US", exception.Message);
        Assert.Equal("welcome", exception.Key);
        Assert.Equal(CultureInfo.CurrentUICulture, exception.Culture);
    }

    [Fact]
    public void Resolve_applies_case_transformation_when_requested()
    {
        InMemoryTranslationStore store = new();
        store.AddTemplate("title", "hello world");
        Translator translator = new(store);

        var result = translator.Get("title")
            .WithCase(LetterCase.Upper)
            .Resolve();

        Assert.Equal("HELLO WORLD", result);
    }

    [Fact]
    public void Resolve_returns_placeholder_for_missing_key_by_default()
    {
        InMemoryTranslationStore store = new();
        Translator translator = new(store);

        var result = translator.Get("missing").Resolve();

        Assert.Equal("[missing]", result);
    }

    [Fact]
    public void Resolve_uses_custom_exception_factory_for_missing_key()
    {
        InMemoryTranslationStore store = new();
        Translator translator = new(
            store,
            new TranslationOptions
            {
                MissingKeyBehavior = MissingTranslationBehavior.ThrowException,
                MissingKeyExceptionFactory = (key, culture) => new TranslationException(
                    key,
                    culture,
                    $"Custom exception for {key} in {culture?.Name ?? "unknown"}")
            });

        var exception = Assert.Throws<TranslationException>(() => translator.Get("missing")
            .WithCulture(new CultureInfo("fr-FR"))
            .Resolve());

        Assert.Equal("Custom exception for missing in fr-FR", exception.Message);
        Assert.Equal("missing", exception.Key);
        Assert.Equal("fr-FR", exception.Culture?.Name);
    }

    [Fact]
    public void Resolve_uses_builder_options_override_for_missing_key()
    {
        InMemoryTranslationStore store = new();
        Translator translator = new(store);

        var result = translator.Get("missing")
            .WithOptions(new TranslationOptions
            {
                MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
                MissingKeyFallbackValue = "Fallback: {key}"
            })
            .Resolve();

        Assert.Equal("Fallback: missing", result);
    }

    [Fact]
    public void Resolve_supports_pluralization_and_genderization()
    {
        InMemoryTranslationStore store = new();
        store.AddTemplate("message", "Count:{quantity}; Gender:{gender}");
        Translator translator = new(store);

        var result = translator.Get("message")
            .Pluralize(3)
            .Genderize(Gender.Female)
            .Resolve();

        Assert.Equal("Count:3; Gender:female", result);
    }

    [Fact]
    public void Resolve_supports_witharg_and_withargs()
    {
        InMemoryTranslationStore store = new();
        store.AddTemplate("greeting", "Hello {name} from {city}!");
        store.AddTemplate("arraySize", "{Length}");
        Translator translator = new(store);

        var greeting = translator.Get("greeting")
            .WithArg("name", "Ada")
            .WithArg("city", "London")
            .Resolve();

        var arraySize = translator.Get("arraySize")
            .WithArg("first", "second")
            .Resolve();

        Assert.Equal("Hello Ada from London!", greeting);
        Assert.Equal("2", arraySize);
    }

    [Theory]
    [MemberData(nameof(CaseTransformationData))]
    public void Resolve_applies_all_case_transformations(LetterCase letterCase, string expected)
    {
        InMemoryTranslationStore store = new();
        store.AddTemplate("title", "hello world from the rain");
        Translator translator = new(store);

        var result = translator.Get("title")
            .WithCase(letterCase)
            .Resolve();

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TranslationBuilder_can_be_converted_to_string()
    {
        InMemoryTranslationStore store = new();
        store.AddTemplate("hello", "Hello!");
        Translator translator = new(store);

        TranslationBuilder builder = translator.Get("hello");
        string asString = builder;

        Assert.Equal("Hello!", builder.ToString());
        Assert.Equal("Hello!", asString);
    }

    [Fact]
    public void WithArgs_applies_multiple_arguments_and_null_is_safe()
    {
        TestTranslationStore store = new();
        store.AddTemplate("greeting", "Hello {name} from {city}!");

        var builder = new TranslationBuilder(store, "greeting")
            .WithArgs(new Dictionary<string, object?>
            {
                ["name"] = "Ada",
                ["city"] = "Rome"
            });

        Assert.Equal("Hello Ada from Rome!", builder.Resolve());

        store.AddTemplate("hello", "Hello");

        var safeBuilder = new TranslationBuilder(store, "hello").WithArgs(null!);
        Assert.Equal("Hello", safeBuilder.Resolve());
    }

    [Fact]
    public void ReplaceFallbackTokens_replaces_key_and_culture_placeholders()
    {
        var builder = new TranslationBuilder(
            new TestTranslationStore(),
            "welcome",
            new TranslationOptions
            {
                MissingKeyFallbackValue = "Value {0}/{1}"
            })
            .WithCulture("de-DE");

        var method = typeof(TranslationBuilder).GetMethod("ReplaceFallbackTokens", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = (string)method.Invoke(builder, ["fallback {key}/{culture}"])!;

        Assert.Equal("fallback welcome/de-DE", result);
    }

    [Fact]
    public void Resolve_supports_pluralization_and_genderization_with_quantity_and_gender_placeholders()
    {
        TestTranslationStore store = new();
        store.AddTemplate("message", "Count:{quantity}; Gender:{gender}");
        var builder = new TranslationBuilder(store, "message")
            .Pluralize(3)
            .Genderize(Gender.Female);

        Assert.Equal("Count:3; Gender:female", builder.Resolve());
    }

    public static TheoryData<LetterCase, string> CaseTransformationData() => new TheoryData<LetterCase, string>
        {
            { LetterCase.AsIs, "hello world from the rain" },
            { LetterCase.Upper, "HELLO WORLD FROM THE RAIN" },
            { LetterCase.Lower, "hello world from the rain" },
            { LetterCase.CamelCase, "helloWorldFromTheRain" },
            { LetterCase.PascalCase, "HelloWorldFromTheRain" },
            { LetterCase.SnakeCase, "hello_world_from_the_rain" },
            { LetterCase.KebabCase, "hello-world-from-the-rain" }
        };

    private sealed class InMemoryTranslationStore : ITranslationStore
    {
        private readonly Dictionary<string, string?> _templates = new(StringComparer.OrdinalIgnoreCase);

        public void AddTemplate(string key, string? template) => _templates[key] = template;

        public string? GetTemplate(string key, CultureInfo culture)
        {
            if (_templates.TryGetValue(key, out var template))
            {
                return template;
            }

            throw new KeyNotFoundException($"No template found for key '{key}'.");
        }

        public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) => Task.FromResult(GetTemplate(key, culture));
    }
}
