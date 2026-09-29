using System.Reflection;

namespace FluentLocalizer.Store.Json;

/// <summary>Loads and caches culture JSON files embedded in an assembly.</summary>
public sealed class EmbeddedJsonStore : JsonTranslationStoreBase
{
    /// <summary>Creates a store for JSON resources embedded in the configured assembly.</summary>
    public EmbeddedJsonStore(EmbeddedJsonStoreOptions? options = null) : base(options ?? new EmbeddedJsonStoreOptions())
    {
        var storeOptions = (EmbeddedJsonStoreOptions)Options;
        if (storeOptions.MaxDocumentBytes < 0) throw new ArgumentOutOfRangeException(nameof(options), "MaxDocumentBytes cannot be negative.");
        var assembly = storeOptions.ResourceAssembly ?? Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var folder = storeOptions.ResourcesPath.Trim('.', '/', '\\').Replace('/', '.').Replace('\\', '.');
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrEmpty(folder) ||
                name.StartsWith(folder + ".", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("." + folder + ".", StringComparison.OrdinalIgnoreCase)));

        var found = false;
        foreach (var resourceName in resourceNames)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream is null) continue;
                if (storeOptions.MaxDocumentBytes > 0 && stream.CanSeek && stream.Length > storeOptions.MaxDocumentBytes)
                    throw new InvalidDataException($"Translation resource '{resourceName}' exceeds MaxDocumentBytes.");
                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                if (storeOptions.MaxDocumentBytes > 0 && System.Text.Encoding.UTF8.GetByteCount(json) > storeOptions.MaxDocumentBytes)
                    throw new InvalidDataException($"Translation resource '{resourceName}' exceeds MaxDocumentBytes.");
                SetDocument(JsonStoreCore.GetCacheKey(resourceName), json);
                found = true;
            }
            catch (Exception ex) when (!storeOptions.ThrowOnMissingStore && (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException))
            { }
        }

        if (!found && storeOptions.ThrowOnMissingStore)
            throw new FileNotFoundException($"No embedded translation JSON files were found in '{assembly.FullName}' under '{storeOptions.ResourcesPath}'.");
    }
}
