using System.Globalization;
using System.Runtime.InteropServices;

namespace FluentLocalizer.Store.Json;

/// <summary>Compatibility wrapper for applications using the original mode-based JSON store API.</summary>
[Obsolete("JsonStore is deprecated. Use JsonFileStore or EmbeddedJsonStore with their specific options.")]
public sealed class JsonStore : ITranslationStore, IDisposable
{
    private readonly ITranslationStore _store;
    private readonly JsonFileStore? _jfs;

    /// <summary>Creates a compatibility store that forwards to the selected dedicated JSON store.</summary>
    public JsonStore(JsonStoreOptions? options = null)
    {
        options ??= new JsonStoreOptions();
        switch (options.SearchMode)
        {
            case JsonStoreLocation.FileSystem:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
                    throw new PlatformNotSupportedException("JsonStore with FileSystem location is not supported in browser applications. Use EmbeddedJsonStore or HttpJsonStore instead.");
                var fileOptions = new JsonFileStoreOptions
                {
                    ResourcesPath = options.ResourcesPath,
                    FallbackCulture = options.FallbackCulture,
                    ThrowOnMissingStore = options.ThrowOnMissingStore,
                    ReloadOnChange = options.ReloadOnChange
                };
                CopyMappings(options, fileOptions);
                var fileStore = new JsonFileStore(fileOptions);
                _store = fileStore;
                _jfs = fileStore;
                break;
            case JsonStoreLocation.EmbeddedResources:
                var embeddedOptions = new EmbeddedJsonStoreOptions
                {
                    ResourcesPath = options.ResourcesPath,
                    ResourceAssembly = options.ResourceAssembly,
                    FallbackCulture = options.FallbackCulture,
                    ThrowOnMissingStore = options.ThrowOnMissingStore
                };
                CopyMappings(options, embeddedOptions);
                _store = new EmbeddedJsonStore(embeddedOptions);
                break;
            default:
                throw new InvalidOperationException($"Unsupported search mode '{options.SearchMode}'.");
        }
    }

    /// <inheritdoc />
    public string? GetTemplate(string key, CultureInfo culture) => _store.GetTemplate(key, culture);

    /// <inheritdoc />
    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) =>
        _store.GetTemplateAsync(key, culture, cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _jfs?.Dispose();

    private static void CopyMappings(JsonStoreSettings source, JsonStoreSettings destination)
    {
        foreach (var mapping in source.FileMappings)
            destination.FileMappings[mapping.Key] = mapping.Value;
    }
}
