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

    [Theory]
    [InlineData("it-IT.common.json", "specific flat namespace")]
    [InlineData("it-IT/common.json", "specific folder namespace")]
    public void Resolves_specific_namespace_file_paths(string file, string expected)
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write(file, "{\"Section\":{\"Key\":\"" + expected + "\"}}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path });

        Assert.Equal(expected, store.GetTemplate("common:Section:Key", new CultureInfo("it-IT")));
    }

    [Theory]
    [InlineData("common.it-IT.json")]
    [InlineData("it-IT.common.json")]
    [InlineData("it-IT/common.json")]
    public void Resolves_all_namespace_file_conventions(string path)
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write(path, "{\"Section\":{\"Title\":\"Titolo\"}}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path });
        Assert.Equal("Titolo", store.GetTemplate("common:Section:Title", new CultureInfo("it-IT")));
    }

    [Fact]
    public void Neutral_request_selects_first_available_specific_culture_and_does_not_cross_regions()
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("en-US.json", "{\"Value\":\"US\"}");
        files.Write("en-GB.json", "{\"Value\":\"GB\"}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path });

        Assert.Equal("GB", store.GetTemplate("Value", new CultureInfo("en")));
        Assert.Equal("US", store.GetTemplate("Value", new CultureInfo("en-US")));
        Assert.Throws<FileNotFoundException>(() => store.GetTemplate("Value", new CultureInfo("en-AU")));
    }

    [Fact]
    public void Combined_file_can_live_inside_its_culture_folder()
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("it-IT/it-IT.json", "{\"Home\":{\"Title\":\"Casa\"}}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path });
        Assert.Equal("Casa", store.GetTemplate("Home:Title", new CultureInfo("it")));
    }

    [Theory]
    [InlineData("it.common.json", "neutral flat namespace")]
    [InlineData("it/common.json", "neutral folder namespace")]
    public void Resolves_neutral_namespace_files(string file, string expected)
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write(file, "{\"Section\":{\"Key\":\"" + expected + "\"}}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path });

        Assert.Equal(expected, store.GetTemplate("common:Section:Key", new CultureInfo("it-IT")));
    }

    [Theory]
    [InlineData("en-US.common.json", "fallback flat namespace")]
    [InlineData("en-US/common.json", "fallback folder namespace")]
    public void Resolves_fallback_namespace_files(string file, string expected)
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write(file, "{\"Section\":{\"Key\":\"" + expected + "\"}}");
        using var store = new JsonFileStore(new JsonFileStoreOptions
        {
            ResourcesPath = files.Path,
            FallbackCulture = "en-US"
        });

        Assert.Equal(expected, store.GetTemplate("common:Section:Key", new CultureInfo("de-DE")));
    }

    [Theory]
    [InlineData("it.json", "it-IT", "neutral combined file")]
    [InlineData("en-US.json", "de-DE", "fallback combined file")]
    public void Resolves_namespaces_inside_neutral_and_fallback_combined_files(string file, string culture, string expected)
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write(file, "{\"common\":{\"Section\":{\"Key\":\"" + expected + "\"}}}");
        using var store = new JsonFileStore(new JsonFileStoreOptions
        {
            ResourcesPath = files.Path,
            FallbackCulture = "en-US"
        });

        Assert.Equal(expected, store.GetTemplate("common:Section:Key", new CultureInfo(culture)));
    }

    [Fact]
    public void Prefers_specific_namespace_then_specific_combined_then_neutral_namespace()
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("it-IT.common.json", "{\"Section\":{\"Key\":\"specific namespace\"}}");
        files.Write("it-IT.json", "{\"common\":{\"Section\":{\"Key\":\"specific combined\"}}}");
        files.Write("it/common.json", "{\"Section\":{\"Key\":\"neutral namespace\"}}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path, ReloadOnChange = true });
        var culture = new CultureInfo("it-IT");

        Assert.Equal("specific namespace", store.GetTemplate("common:Section:Key", culture));

        File.Delete(Path.Combine(files.Path, "it-IT.common.json"));
        AssertEventually(() => Assert.Equal("specific combined", store.GetTemplate("common:Section:Key", culture)));

        File.Delete(Path.Combine(files.Path, "it-IT.json"));
        AssertEventually(() => Assert.Equal("neutral namespace", store.GetTemplate("common:Section:Key", culture)));
    }

    [Fact]
    public void Keeps_namespaced_and_legacy_nested_keys_separate()
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("it-IT.json", "{\"Home\":{\"Welcome\":\"nested\"},\"common\":{\"Home\":{\"Welcome\":\"combined namespace\"}}}");
        files.Write("it-IT.common.json", "{\"Home\":{\"Welcome\":\"split namespace\"}}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path });
        var culture = new CultureInfo("it-IT");

        Assert.Equal("split namespace", store.GetTemplate("common:Home:Welcome", culture));
        Assert.Equal("nested", store.GetTemplate("Home:Welcome", culture));
        Assert.Null(store.GetTemplate("other:Home:Welcome", culture));
    }

    [Fact]
    public void Supports_nested_file_mappings_for_namespaced_keys()
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("mapped/italian.json", "{\"common\":{\"Section\":{\"Key\":\"mapped\"}}}");
        var options = new JsonFileStoreOptions { ResourcesPath = files.Path };
        options.FileMappings["it-IT"] = "mapped/italian.json";
        using var store = new JsonFileStore(options);

        Assert.Equal("mapped", store.GetTemplate("common:Section:Key", new CultureInfo("it-IT")));
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
    [InlineData("../outside.json")]
    [InlineData("nested/../../outside.json")]
    [InlineData("/outside.json")]
    public void Rejects_mappings_outside_the_resource_directory(string mapping)
    {
        using var files = new TemporaryLocaleDirectory();
        var options = new JsonFileStoreOptions { ResourcesPath = files.Path };
        options.FileMappings["it-IT"] = mapping;
        Assert.Throws<ArgumentException>(() => new JsonFileStore(options));
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void Rejects_a_symbolic_link_to_a_json_file_outside_resources_path()
    {
        using var files = new TemporaryLocaleDirectory();
        using var outside = new TemporaryLocaleDirectory();
        var target = outside.Write("secret.json", "{\"Value\":\"secret\"}");
        try { File.CreateSymbolicLink(Path.Combine(files.Path, "en-US.json"), target); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException) { return; }
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path });
        Assert.Throws<System.Security.SecurityException>(() => store.GetTemplate("Value", new CultureInfo("en-US")));
    }
#endif

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
    public void File_document_limit_rejects_large_catalogs()
    {
        using var files = new TemporaryLocaleDirectory();
        files.Write("en-US.json", "{\"Value\":\"too large\"}");
        using var unlimited = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path, MaxDocumentBytes = 0 });
        Assert.Equal("too large", unlimited.GetTemplate("Value", new CultureInfo("en-US")));
        Assert.Throws<InvalidDataException>(() => new JsonFileStore(new JsonFileStoreOptions
        {
            ResourcesPath = files.Path,
            MaxDocumentBytes = 8,
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
        AssertEventually(() => Assert.Throws<FileNotFoundException>(() => store.GetTemplate("Value", new CultureInfo("de-DE"))));
    }

    [Fact]
    public void Reload_on_change_handles_namespace_files_in_culture_folders()
    {
        using var files = new TemporaryLocaleDirectory();
        var path = files.Write("it-IT/common.json", "{\"Section\":{\"Key\":\"first\"}}");
        using var store = new JsonFileStore(new JsonFileStoreOptions { ResourcesPath = files.Path, ReloadOnChange = true });
        var culture = new CultureInfo("it-IT");
        const string key = "common:Section:Key";

        Assert.Equal("first", store.GetTemplate(key, culture));
        File.WriteAllText(path, "{\"Section\":{\"Key\":\"updated\"}}");
        AssertEventually(() => Assert.Equal("updated", store.GetTemplate(key, culture)));

        File.Delete(path);
        AssertEventually(() => Assert.Null(store.GetTemplate(key, culture)));
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
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file)!);
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
