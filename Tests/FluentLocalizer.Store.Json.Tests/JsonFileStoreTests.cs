using System.Globalization;
using FluentLocalizer.Store.Json;

namespace FluentLocalizer.Test;

public sealed class JsonFileStoreTests
{
    [Fact]
    public async Task Loads_files_and_supports_synchronous_and_async_lookups()
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("it-IT.json", "{\"Hello\":\"Ciao\"}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path });

        Assert.Equal("Ciao", store.GetTemplate("Hello", new CultureInfo("it-IT")));
        Assert.Equal("Ciao", await store.GetTemplateAsync("Hello", new CultureInfo("it-IT"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Resolves_specific_neutral_and_fallback_files_in_order()
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("fr-FR.json", "{\"Value\":\"specific\"}");
        files.Write("fr.json", "{\"Value\":\"neutral\"}");
        files.Write("en-US.json", "{\"Value\":\"fallback\"}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path, FallbackCulture = "en-US" });

        Assert.Equal("specific", store.GetTemplate("Value", new CultureInfo("fr-FR")));
        Assert.Equal("neutral", store.GetTemplate("Value", new CultureInfo("fr-CA")));
        Assert.Equal("fallback", store.GetTemplate("Value", new CultureInfo("de-DE")));
    }

    [Fact]
    public void Uses_file_mapping_before_standard_culture_file()
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("custom.json", "{\"Value\":\"mapped\"}");
        files.Write("it-IT.json", "{\"Value\":\"standard\"}");
        var options = new JsonFileStoreOptions { ResourcesPath = files.Path };
        options.FileMappings["it-IT"] = "custom.json";
        using var store = new JsonFileStore(options);

        Assert.Equal("mapped", store.GetTemplate("Value", new CultureInfo("it-IT")));
    }

    [Theory]
    [InlineData("Home: Welcome ", "Benvenuto")]
    [InlineData("Home::Welcome", "Benvenuto")]
    [InlineData("Missing", null)]
    [InlineData("Numeric", null)]
    [InlineData(" ", null)]
    public void Looks_up_nested_keys_and_returns_null_for_non_string_or_missing_values(string key, string? expected)
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("it-IT.json", "{\"Home\":{\"Welcome\":\"Benvenuto\"},\"Numeric\":12}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path });

        Assert.Equal(expected, store.GetTemplate(key, new CultureInfo("it-IT")));
    }

    [Fact]
    public void Missing_store_errors_are_controlled_by_option()
    {
        using var missing = new TemporaryLocaleDirectory(create: false);
        using var quiet = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = missing.Path });
        Assert.Null(quiet.GetTemplate("Value", new CultureInfo("it-IT")));

        Assert.Throws<FileNotFoundException>(() => new JsonFileStore(new JsonFileStoreOptions
        {
            ResourcesPath = missing.Path,
            ThrowOnMissingStore = true
        }));

        using var empty = new TemporaryLocaleDirectory();
        Assert.Throws<FileNotFoundException>(() => new JsonFileStore(new JsonFileStoreOptions
        {
            ResourcesPath = empty.Path,
            ThrowOnMissingStore = true
        }));
    }

    [Fact]
    public void Invalid_json_is_skipped_by_default_and_throws_when_configured()
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("it-IT.json", "{not-json");
        using var quiet = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path });
        Assert.Null(quiet.GetTemplate("Value", new CultureInfo("it-IT")));

        Assert.ThrowsAny<System.Text.Json.JsonException>(() => new JsonFileStore(new JsonFileStoreOptions
        {
            ResourcesPath = files.Path,
            ThrowOnMissingStore = true
        }));
    }

    [Fact]
    public void Reload_on_change_handles_write_create_delete_and_rename()
    {
        using var files = new TemporaryLocaleDirectory();
        var first = files.Write("it-IT.json", "{\"Value\":\"first\"}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path, ReloadOnChange = true });
        var culture = new CultureInfo("it-IT");
        Assert.Equal("first", store.GetTemplate("Value", culture));

        File.WriteAllText(first, "{\"Value\":\"updated\"}");
        AssertEventually(() => Assert.Equal("updated", store.GetTemplate("Value", culture)));

        var created = files.Write("fr-FR.json", "{\"Value\":\"created\"}");
        AssertEventually(() => Assert.Equal("created", store.GetTemplate("Value", new CultureInfo("fr-FR"))));
        File.Delete(created);
        AssertEventually(() => Assert.Null(store.GetTemplate("Value", new CultureInfo("fr-FR"))));

        var renamed = files.Write("de-DE.json", "{\"Value\":\"renamed\"}");
        AssertEventually(() => Assert.Equal("renamed", store.GetTemplate("Value", new CultureInfo("de-DE"))));
        MoveEventually(renamed, Path.Combine(files.Path, "de-AT.json"));
        AssertEventually(() => Assert.Equal("renamed", store.GetTemplate("Value", new CultureInfo("de-AT"))));
        AssertEventually(() => Assert.Null(store.GetTemplate("Value", new CultureInfo("de-DE"))));
    }

    private static void AssertEventually(Action assertion)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        Exception? lastError = null;
        while (DateTime.UtcNow < deadline)
        {
            try { assertion(); return; }
            catch (Exception exception) { lastError = exception; Thread.Sleep(50); }
        }
        throw new Xunit.Sdk.XunitException($"Condition was not met before timeout. {lastError}");
    }

    private static void MoveEventually(string source, string destination)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (true)
        {
            try { File.Move(source, destination); return; }
            catch (IOException) when (DateTime.UtcNow < deadline) { Thread.Sleep(50); }
        }
    }
}

internal sealed class TemporaryLocaleDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"fluent-localizer-{Guid.NewGuid():N}");

    public TemporaryLocaleDirectory(bool create = true)
    {
        if (create) Directory.CreateDirectory(Path);
    }

    public string Write(string name, string json)
    {
        Directory.CreateDirectory(Path);
        var file = System.IO.Path.Combine(Path, name);
        File.WriteAllText(file, json);
        return file;
    }

    public void Dispose()
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (Directory.Exists(Path))
        {
            try { Directory.Delete(Path, recursive: true); return; }
            catch (IOException) when (DateTime.UtcNow < deadline) { Thread.Sleep(50); }
        }
    }
}
