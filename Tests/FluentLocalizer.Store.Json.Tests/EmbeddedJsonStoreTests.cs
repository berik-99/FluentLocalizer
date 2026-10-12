using System.Globalization;
using System.Text.Json;
using FluentLocalizer.Store.Json;

namespace FluentLocalizer.Test;

public sealed class EmbeddedJsonStoreTests
{
    [Fact]
    public async Task Loads_resources_and_supports_sync_and_async_lookups()
    {
        var store = new EmbeddedJsonStore(new EmbeddedJsonStoreOptions
        {
            ResourcesPath = "Locales",
            ResourceAssembly = typeof(EmbeddedJsonStoreTests).Assembly
        });

        Assert.Equal("Hello from embedded resource", store.GetTemplate("Hello", new CultureInfo("en-US")));
        Assert.Equal("Hello from embedded resource", await store.GetTemplateAsync("Hello", new CultureInfo("en-US"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Selects_resources_by_folder_and_honors_mappings_and_fallback()
    {
        var store = new EmbeddedJsonStore(new EmbeddedJsonStoreOptions
        {
            ResourcesPath = "Locales",
            ResourceAssembly = typeof(EmbeddedJsonStoreTests).Assembly,
            FallbackCulture = "en-US"
        });

        Assert.Equal("Hello from embedded resource", store.GetTemplate("Hello", new CultureInfo("fr-FR")));
    }

    [Fact]
    public void Namespace_files_of_different_cultures_keep_separate_documents()
    {
        var options = Options("EmbeddedLocales/Namespaces");
        options.FileMappings["it-IT"] = "it.Ui.json";
        options.FileMappings["de-DE"] = "de.Extra.json";
        options.FileMappings["fr-FR"] = "it.Extra.json";
        var store = new EmbeddedJsonStore(options);

        Assert.Equal("Titolo", store.GetTemplate("Title", new CultureInfo("it-IT")));
        Assert.Equal("Ciao", store.GetTemplate("Hello", new CultureInfo("it-IT")));
        Assert.Equal("German extra", store.GetTemplate("Extra", new CultureInfo("de-DE")));
        Assert.Equal("Italian extra", store.GetTemplate("Extra", new CultureInfo("fr-FR")));
    }

    [Fact]
    public void Namespace_files_stay_reachable_by_their_previous_file_name()
    {
        var options = Options("EmbeddedLocales/Namespaces");
        options.FileMappings["it-IT"] = "Ui.json";

        Assert.Equal("Titolo", new EmbeddedJsonStore(options).GetTemplate("Title", new CultureInfo("it-IT")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Namespace_files_are_searched_before_the_culture_file_and_fall_back_per_key(bool loadOnDemand)
    {
        var options = Options("EmbeddedLocales/Namespaces");
        options.LoadOnDemand = loadOnDemand;
        var store = new EmbeddedJsonStore(options);

        Assert.Equal("Titolo", store.GetTemplate("Ui:Title", new CultureInfo("it-IT")));
        Assert.Equal("Titolo", store.GetTemplate(" Ui : Title ", new CultureInfo("it")));
        Assert.Equal("Sottotitolo", store.GetTemplate("Ui:Subtitle", new CultureInfo("it-IT")));
        Assert.Equal("Italian extra", store.GetTemplate("Extra:Extra", new CultureInfo("it-IT")));
        Assert.Equal("German extra", store.GetTemplate("Extra:Extra", new CultureInfo("de-DE")));
        Assert.Equal("Spanish extra", store.GetTemplate("Extra:Extra", new CultureInfo("es-ES")));
        Assert.Equal("English title", store.GetTemplate("Ui:Title", new CultureInfo("de-DE")));
        Assert.Equal("English title", store.GetTemplate("Ui:Title", new CultureInfo("en")));
        Assert.Null(store.GetTemplate("Extra:Extra", new CultureInfo("fr-FR")));
        Assert.Null(store.GetTemplate("Ui:Missing", new CultureInfo("it-IT")));
    }

    [Fact]
    public void Namespaced_and_plain_keys_share_one_store()
    {
        var store = new EmbeddedJsonStore(Options("EmbeddedLocales/Namespaces"));

        Assert.Equal("Titolo", store.GetTemplate("Ui:Title", new CultureInfo("it-IT")));
        Assert.Equal("Ciao", store.GetTemplate("Hello", new CultureInfo("it-IT")));
        Assert.Equal("Only English", store.GetTemplate("OnlyEnglish", new CultureInfo("it-IT")));
    }

    [Fact]
    public void Load_on_demand_reads_a_namespace_file_only_for_its_keys()
    {
        var options = Options("EmbeddedLocales/Broken");
        options.ThrowOnMissingStore = true;
        options.LoadOnDemand = true;
        var store = new EmbeddedJsonStore(options);

        Assert.Equal("Ciao", store.GetTemplate("Hello", new CultureInfo("it-IT")));
        Assert.ThrowsAny<JsonException>(() => store.GetTemplate("Docs:Intro", new CultureInfo("it-IT")));
    }

    [Fact]
    public void A_culture_with_only_namespace_files_counts_as_available()
    {
        var options = Options("EmbeddedLocales/Namespaces");
        options.ThrowOnMissingStore = true;
        options.FallbackCulture = "de";

        Assert.Null(new EmbeddedJsonStore(options).GetTemplate("Missing", new CultureInfo("ja-JP")));

        options.FallbackCulture = "ja";
        Assert.Throws<FileNotFoundException>(() => new EmbeddedJsonStore(options).GetTemplate("Missing", new CultureInfo("ko-KR")));
    }

    [Fact]
    public void Invalid_resources_throw_during_construction_by_default()
    {
        var options = Options("EmbeddedLocales/Broken");
        options.ThrowOnMissingStore = true;

        Assert.ThrowsAny<JsonException>(() => new EmbeddedJsonStore(options));
    }

    [Fact]
    public void Load_on_demand_parses_only_the_files_a_lookup_needs()
    {
        var options = Options("EmbeddedLocales/Broken");
        options.ThrowOnMissingStore = true;
        options.LoadOnDemand = true;
        var store = new EmbeddedJsonStore(options);

        Assert.Equal("Ciao", store.GetTemplate("Hello", new CultureInfo("it-IT")));
        Assert.ThrowsAny<JsonException>(() => store.GetTemplate("Missing", new CultureInfo("it-IT")));
    }

    [Fact]
    public void Load_on_demand_skips_invalid_resources_when_missing_stores_are_allowed()
    {
        var options = Options("EmbeddedLocales/Broken");
        options.LoadOnDemand = true;
        var store = new EmbeddedJsonStore(options);

        Assert.Null(store.GetTemplate("Missing", new CultureInfo("it-IT")));
        Assert.Null(store.GetTemplate("Hello", new CultureInfo("en")));
    }

    [Fact]
    public void Load_on_demand_checks_the_size_limit_during_construction()
    {
        var options = Options("EmbeddedLocales/Namespaces");
        options.LoadOnDemand = true;
        options.MaxDocumentBytes = 5;

        Assert.Throws<InvalidDataException>(() => new EmbeddedJsonStore(options));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reads_utf16_resources(bool loadOnDemand)
    {
        var options = Options("EmbeddedLocales/Utf16");
        options.LoadOnDemand = loadOnDemand;

        Assert.Equal("Hello from UTF-16", new EmbeddedJsonStore(options).GetTemplate("Hello", new CultureInfo("en")));
    }

    [Fact]
    public void Throws_if_no_resources_are_found_when_requested()
    {
        Assert.Throws<FileNotFoundException>(() => new EmbeddedJsonStore(new EmbeddedJsonStoreOptions
        {
            ResourcesPath = "NoSuchFolder",
            ResourceAssembly = typeof(EmbeddedJsonStoreTests).Assembly,
            ThrowOnMissingStore = true
        }));
    }

    private static EmbeddedJsonStoreOptions Options(string resourcesPath) => new()
    {
        ResourcesPath = resourcesPath,
        ResourceAssembly = typeof(EmbeddedJsonStoreTests).Assembly,
        FallbackCulture = "en"
    };
}
