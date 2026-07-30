using System.Globalization;
using FluentLocalizer.Store.Json;

namespace FluentLocalizer.Test;

public class JsonTranslationStoreTests
{
    [Fact]
    public void GetTemplate_LoadsTranslationsFromFilesystem()
    {
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            var filePath = Path.Combine(temporaryDirectory, "it-IT.json");
            File.WriteAllText(filePath, "{\"Hello\":\"Ciao\"}");

            var options = new JsonStoreOptions
            {
                ResourcesPath = temporaryDirectory,
                SearchMode = JsonStoreLocation.FileSystem
            };

            using JsonStore store = new(options);

            Assert.Equal("Ciao", store.GetTemplate("Hello", new CultureInfo("it-IT")));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void GetTemplate_LoadsTranslationsFromEmbeddedResources()
    {
        var options = new JsonStoreOptions
        {
            SearchMode = JsonStoreLocation.EmbeddedResources,
            ResourceAssembly = typeof(JsonTranslationStoreTests).Assembly
        };

        using JsonStore store = new(options);

        Assert.Equal("Hello from embedded resource", store.GetTemplate("Hello", new CultureInfo("en-US")));
    }

    [Fact]
    public void GetTemplate_ResolvesNestedKeysUsingColonDelimiter()
    {
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            var filePath = Path.Combine(temporaryDirectory, "it-IT.json");
            File.WriteAllText(filePath, "{\"Home\":{\"Welcome\":\"Benvenuto\"}}");

            var options = new JsonStoreOptions
            {
                ResourcesPath = temporaryDirectory,
                SearchMode = JsonStoreLocation.FileSystem
            };

            using JsonStore store = new(options);

            Assert.Equal("Benvenuto", store.GetTemplate("Home:Welcome", new CultureInfo("it-IT")));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void GetTemplate_UsesFallbackCultureWhenRequestedCultureIsMissing()
    {
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(Path.Combine(temporaryDirectory, "en-US.json"), "{\"Hello\":\"Hello\"}");

            var options = new JsonStoreOptions
            {
                ResourcesPath = temporaryDirectory,
                SearchMode = JsonStoreLocation.FileSystem,
                FallbackCulture = "en-US"
            };

            using JsonStore store = new(options);

            Assert.Equal("Hello", store.GetTemplate("Hello", new CultureInfo("fr-FR")));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void GetTemplate_UsesConfiguredFileMappings()
    {
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(Path.Combine(temporaryDirectory, "english.json"), "{\"Hello\":\"Hello from mapped file\"}");

            var options = new JsonStoreOptions
            {
                ResourcesPath = temporaryDirectory,
                SearchMode = JsonStoreLocation.FileSystem,
                FallbackCulture = "en-US"
            };
            options.FileMappings["en-US"] = "english.json";

            using JsonStore store = new(options);

            Assert.Equal("Hello from mapped file", store.GetTemplate("Hello", new CultureInfo("en-US")));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public async Task GetTemplateAsync_ReturnsTemplateForTheRequestedCulture()
    {
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            var filePath = Path.Combine(temporaryDirectory, "de-DE.json");
            File.WriteAllText(filePath, "{\"Hello\":\"Hallo\"}");

            var options = new JsonStoreOptions
            {
                ResourcesPath = temporaryDirectory,
                SearchMode = JsonStoreLocation.FileSystem
            };

            using JsonStore store = new(options);

            var template = await store.GetTemplateAsync("Hello", new CultureInfo("de-DE"), TestContext.Current.CancellationToken);

            Assert.Equal("Hallo", template);
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void GetTemplate_ReturnsNullWhenKeyIsMissing()
    {
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(Path.Combine(temporaryDirectory, "it-IT.json"), "{\"Hello\":\"Ciao\"}");

            var options = new JsonStoreOptions
            {
                ResourcesPath = temporaryDirectory,
                SearchMode = JsonStoreLocation.FileSystem
            };

            using JsonStore store = new(options);

            Assert.Null(store.GetTemplate("Goodbye", new CultureInfo("it-IT")));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void ReloadOnChange_UpdatesTemplatesWhenFileChanges()
    {
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            var filePath = Path.Combine(temporaryDirectory, "it-IT.json");
            File.WriteAllText(filePath, "{\"Hello\":\"Ciao\"}");

            var options = new JsonStoreOptions
            {
                ResourcesPath = temporaryDirectory,
                SearchMode = JsonStoreLocation.FileSystem,
                ReloadOnChange = true
            };

            using JsonStore store = new(options);
            var culture = new CultureInfo("it-IT");

            Assert.Equal("Ciao", store.GetTemplate("Hello", culture));

            File.WriteAllText(filePath, "{\"Hello\":\"Buongiorno\"}");

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline)
            {
                if (store.GetTemplate("Hello", culture) == "Buongiorno")
                {
                    return;
                }

                Thread.Sleep(100);
            }

            Assert.Equal("Buongiorno", store.GetTemplate("Hello", culture));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void Constructor_ThrowsWhenTranslationFilesAreMissingAndThrowOnErrorIsEnabled()
    {
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            var options = new JsonStoreOptions
            {
                ResourcesPath = temporaryDirectory,
                SearchMode = JsonStoreLocation.FileSystem,
                ThrowOnError = true
            };

            Assert.Throws<FileNotFoundException>(() => new JsonStore(options));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    [Fact]
    public void Constructor_ThrowsWhenTranslationFileIsInvalidAndThrowOnErrorIsEnabled()
    {
        var temporaryDirectory = CreateTemporaryDirectory();

        try
        {
            var filePath = Path.Combine(temporaryDirectory, "it-IT.json");
            File.WriteAllText(filePath, "{invalid json");

            var options = new JsonStoreOptions
            {
                ResourcesPath = temporaryDirectory,
                SearchMode = JsonStoreLocation.FileSystem,
                ThrowOnError = true
            };

            Assert.ThrowsAny<Exception>(() => new JsonStore(options));
        }
        finally
        {
            DeleteTemporaryDirectory(temporaryDirectory);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fluent-localizer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }
}
