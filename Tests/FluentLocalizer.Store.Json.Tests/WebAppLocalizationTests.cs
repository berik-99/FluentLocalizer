using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentLocalizer.Store.Json;
using FluentLocalizer.WebApp;

namespace FluentLocalizer.Test;

public sealed class WebAppLocalizationTests
{
    [Theory]
    [InlineData("it-CH", "it-CH")]
    [InlineData("fr-CA", "fr-CA")]
    [InlineData("de-AT", "de-AT")]
    [InlineData("es-MX", "es-MX")]
    [InlineData("en-GB", "en-GB")]
    [InlineData("it", "it")]
    [InlineData("en", "en")]
    [InlineData("fr", "fr")]
    [InlineData("de", "de")]
    [InlineData("es", "es")]
    [InlineData("ja-JP", "en")]
    [InlineData("", "en")]
    [InlineData("not a locale!", "en")]
    public void Selects_supported_browser_cultures_or_english(string language, string expected) =>
        Assert.Equal(expected, BrowserCulture.SelectCulture([language]).Name);

    [Fact]
    public void Uses_browser_preference_order_and_handles_missing_languages()
    {
        Assert.Equal("fr-CA", BrowserCulture.SelectCulture(["ja-JP", "fr-CA", "it-IT"]).Name);
        Assert.Equal("en-US", BrowserCulture.SelectCulture(["en-US", "it-IT"]).Name);
        Assert.Equal("en", BrowserCulture.SelectCulture(null).Name);
        Assert.Equal("en", BrowserCulture.SelectCulture([]).Name);
    }

    [Fact]
    public void All_site_catalogs_have_matching_keys_and_resolve_with_the_real_formatter()
    {
        var store = new EmbeddedJsonStore(new EmbeddedJsonStoreOptions
        {
            ResourcesPath = "WebLocales",
            ResourceAssembly = typeof(WebAppLocalizationTests).Assembly,
            FallbackCulture = "en",
            ThrowOnMissingStore = true
        });
        var translator = new Translator(store, new TranslationOptions
        {
            MissingKeyBehavior = MissingTranslationBehavior.ThrowException,
            FormattingErrorBehavior = FormattingErrorBehavior.ThrowException
        });
        var arguments = new Dictionary<string, object?>
        {
            ["name"] = "Ada", ["package"] = "FluentLocalizer",
            ["culture"] = "it-IT", ["language"] = "Italiano", ["quantity"] = 25,
            ["version"] = "1.0.0", ["date"] = "2026-10-02"
        };

        string[]? expectedKeys = null;
        // Guides link to a versioned documentation set, docs/{scope}/v{major}, so frozen sets keep working.
        var scopes = new[] { "core", "store-json" };
        var itemResults = new[] { "You have 3 items.", "Hai 3 elementi.", "Vous avez 3 éléments.", "Du hast 3 Elemente.", "Tienes 3 elementos." };
        var index = 0;
        foreach (var language in new[] { "en", "it", "fr", "de", "es" })
        {
            using var stream = typeof(WebAppLocalizationTests).Assembly.GetManifestResourceStream(
                $"FluentLocalizer.WebApp.Tests.WebLocales.{language}.json");
            Assert.NotNull(stream);
            using var catalog = JsonDocument.Parse(stream);
            // The interface strings and every nested documentation page are checked alike.
            var entries = Leaves("Ui", catalog.RootElement.GetProperty("Ui"))
                .Concat(Leaves("Docs", catalog.RootElement.GetProperty("Docs"))).ToArray();
            var keys = entries.Select(entry => entry.Key).OrderBy(key => key, StringComparer.Ordinal).ToArray();
            expectedKeys ??= keys;
            Assert.Equal(expectedKeys, keys);

            foreach (var (key, template) in entries)
            {
                Assert.False(string.IsNullOrWhiteSpace(template), $"{language}.json {key} is empty.");
                foreach (Match link in Regex.Matches(template, @"\]\((?<href>docs[^)]*)\)"))
                {
                    var route = Regex.Match(link.Groups["href"].Value, @"^docs/(?<scope>[a-z0-9-]+)/v\d+(/|#|$)");
                    Assert.True(route.Success && scopes.Contains(route.Groups["scope"].Value), $"{language}.json {key} links to {link.Groups["href"].Value}");
                }
                var request = translator.Get(key).WithCulture(language);
                foreach (var argument in arguments) request.WithArg(argument.Key, argument.Value);
                // Quoted '{...}' spans are literal MessageFormat text; every other {name} must be a known argument.
                var expected = Regex.Replace(template, @"'([{}][^']*)'|\{(\w+)\}", match =>
                    match.Groups[1].Success ? match.Groups[1].Value : arguments[match.Groups[2].Value]!.ToString()!);
                Assert.Equal(expected, request.Resolve());

                // Display casing belongs to WithCase, so catalogs never store text in capitals.
                var letters = template.Where(char.IsLetter).ToArray();
                Assert.False(letters.Length > 3 && letters.All(char.IsUpper), $"{language}.json {key} is written in capitals.");
            }

            var demo = catalog.RootElement.GetProperty("Demo");
            Assert.Equal(new[] { "Items", "Welcome" }, demo.EnumerateObject().Select(entry => entry.Name).OrderBy(key => key));
            var culture = CultureInfo.GetCultureInfo(language);
            Assert.Equal(demo.GetProperty("Welcome").GetString()!.Replace("{name}", "Ada"),
                translator.Get("Demo:Welcome").WithCulture(culture).WithArg("name", "Ada").Resolve());
            Assert.Equal(itemResults[index++], translator.Get("Demo:Items").WithCulture(culture).Pluralize(3).Resolve());
        }

        var french = CultureInfo.GetCultureInfo("fr-CA");
        Assert.Equal("Présentation", translator.Get("Ui:Overview").WithCulture(french).Resolve());
        Assert.Equal("POURQUOI FLUENTLOCALIZER", translator.Get("Ui:Why").WithCulture(french).WithCase(LetterCase.Upper).Resolve());
        Assert.Equal("Supprimer l'argument Ada", translator.Get("Ui:RemoveArgument").WithCulture(french).WithArg("name", "Ada").Resolve());
        Assert.Equal("Overview", translator.Get("Ui:Overview").WithCulture("ja-JP").Resolve());
        Assert.Equal("Introduzione", translator.Get("Docs:CoreV1:Intro:Nav").WithCulture("it-CH").Resolve());
    }

    // Every string of a catalog object with its colon-separated key, such as Docs:Templates:Title.
    private static IEnumerable<(string Key, string Value)> Leaves(string path, JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = path + ":" + property.Name;
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var leaf in Leaves(key, property.Value)) yield return leaf;
            }
            else
            {
                Assert.Equal(JsonValueKind.String, property.Value.ValueKind);
                yield return (key, property.Value.GetString()!);
            }
        }
    }
}
