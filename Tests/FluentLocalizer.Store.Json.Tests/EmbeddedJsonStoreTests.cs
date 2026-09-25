using System.Globalization;
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
    public void Throws_if_no_resources_are_found_when_requested()
    {
        Assert.Throws<FileNotFoundException>(() => new EmbeddedJsonStore(new EmbeddedJsonStoreOptions
        {
            ResourcesPath = "NoSuchFolder",
            ResourceAssembly = typeof(EmbeddedJsonStoreTests).Assembly,
            ThrowOnMissingStore = true
        }));
    }
}
