using System.Globalization;
using System.Text.Json;
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
        Assert.Equal(expected, SiteText.SelectCulture([language]).Name);

    [Fact]
    public void Uses_browser_preference_order_and_handles_missing_languages()
    {
        Assert.Equal("fr-CA", SiteText.SelectCulture(["ja-JP", "fr-CA", "it-IT"]).Name);
        Assert.Equal("en-US", SiteText.SelectCulture(["en-US", "it-IT"]).Name);
        Assert.Equal("en", SiteText.SelectCulture(null).Name);
        Assert.Equal("en", SiteText.SelectCulture([]).Name);
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
            ["culture"] = "it-IT", ["language"] = "Italiano"
        };

        string[]? expectedKeys = null;
        var itemResults = new[] { "You have 3 items.", "Hai 3 elementi.", "Vous avez 3 éléments.", "Du hast 3 Elemente.", "Tienes 3 elementos." };
        var index = 0;
        foreach (var language in new[] { "en", "it", "fr", "de", "es" })
        {
            using var stream = typeof(WebAppLocalizationTests).Assembly.GetManifestResourceStream(
                $"FluentLocalizer.WebApp.Tests.WebLocales.{language}.json");
            Assert.NotNull(stream);
            using var catalog = JsonDocument.Parse(stream);
            var entries = catalog.RootElement.GetProperty("Ui").EnumerateObject().ToArray();
            var keys = entries.Select(entry => entry.Name).OrderBy(key => key).ToArray();
            expectedKeys ??= keys;
            Assert.Equal(expectedKeys, keys);

            foreach (var entry in entries)
            {
                var template = entry.Value.GetString();
                Assert.False(string.IsNullOrWhiteSpace(template));
                var request = translator.Get("Ui:" + entry.Name).WithCulture(language);
                foreach (var argument in arguments) request.WithArg(argument.Key, argument.Value);
                var expected = template!;
                foreach (var argument in arguments)
                    expected = expected.Replace("{" + argument.Key + "}", argument.Value!.ToString());
                Assert.Equal(expected, request.Resolve());
            }

            var demo = catalog.RootElement.GetProperty("Demo");
            Assert.Equal(new[] { "Items", "Welcome" }, demo.EnumerateObject().Select(entry => entry.Name).OrderBy(key => key));
            var textForCulture = new SiteText(translator) { Culture = CultureInfo.GetCultureInfo(language) };
            Assert.Equal(demo.GetProperty("Welcome").GetString()!.Replace("{name}", "Ada"), textForCulture.Format("Demo:Welcome", "name", "Ada"));
            Assert.Equal(itemResults[index++], textForCulture.Format("Demo:Items", "quantity", 3));
        }

        var text = new SiteText(translator) { Culture = CultureInfo.GetCultureInfo("fr-CA") };
        Assert.Equal("Présentation", text["Ui:Overview"]);
        Assert.Equal("Supprimer l'argument Ada", text.Format("Ui:RemoveArgument", "name", "Ada"));
        text.Culture = CultureInfo.GetCultureInfo("ja-JP");
        Assert.Equal("Overview", text["Ui:Overview"]);
    }
}
