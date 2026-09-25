using System.Reflection;

namespace FluentLocalizer.Store.Json;

/// <summary>Loads and caches culture JSON files embedded in an assembly.</summary>
public sealed class EmbeddedJsonStore : JsonTranslationStoreBase
{
    /// <summary>Creates a store for JSON resources embedded in the configured assembly.</summary>
    public EmbeddedJsonStore(EmbeddedJsonStoreOptions? options = null) : base(options ?? new EmbeddedJsonStoreOptions())
    {
        var storeOptions = (EmbeddedJsonStoreOptions)Options;
        var assembly = storeOptions.ResourceAssembly ?? Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var folder = storeOptions.ResourcesPath.Trim('.', '/', '\\').Replace('/', '.').Replace('\\', '.');
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Where(name => string.IsNullOrEmpty(folder) ||
                name.StartsWith(folder + ".", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("." + folder + ".", StringComparison.OrdinalIgnoreCase) >= 0);

        var found = false;
        foreach (var resourceName in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null) continue;
                using var reader = new StreamReader(stream);
                SetDocument(JsonStoreCore.GetCacheKey(resourceName), reader.ReadToEnd());
                found = true;
            }
            catch (Exception ex) when (!storeOptions.ThrowOnMissingStore && (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException))
            { }
        }

        if (!found && storeOptions.ThrowOnMissingStore)
            throw new FileNotFoundException($"No embedded translation JSON files were found in '{assembly.FullName}' under '{storeOptions.ResourcesPath}'.");
    }
}
