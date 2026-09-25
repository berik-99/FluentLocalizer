using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace FluentLocalizer.Store.Json;

/// <summary>Base class for JSON stores that share culture fallback and nested-key lookup.</summary>
public abstract class JsonTranslationStoreBase(JsonStoreSettings options) : ITranslationStore
{
    private readonly ConcurrentDictionary<string, JsonElement> _documents = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets the shared fallback and file mapping settings used by this store.</summary>
    protected JsonStoreSettings Options { get; } = options;

    /// <summary>Returns candidate JSON file names in culture and fallback order.</summary>
    protected IReadOnlyList<string> ResolveCandidates(CultureInfo culture) =>
        JsonStoreCore.ResolveCandidates(culture, Options.FallbackCulture, Options.FileMappings);

    /// <summary>Parses and caches a JSON document under its file name.</summary>
    /// <param name="name">The file name used during culture resolution.</param>
    /// <param name="json">The JSON document contents.</param>
    protected void SetDocument(string name, string json)
    {
        using var document = JsonDocument.Parse(json);
        _documents[name] = document.RootElement.Clone();
    }

    /// <summary>Clones and caches a parsed JSON root element.</summary>
    /// <param name="name">The file name used during culture resolution.</param>
    /// <param name="root">The JSON root element.</param>
    protected void SetDocument(string name, JsonElement root) => _documents[name] = root.Clone();

    /// <summary>Removes a cached JSON document.</summary>
    /// <param name="name">The cached file name.</param>
    protected void RemoveDocument(string name) => _documents.TryRemove(name, out _);

    /// <summary>Checks whether any candidate JSON document is cached for a culture.</summary>
    /// <param name="culture">The culture to check.</param>
    protected bool HasCandidate(CultureInfo culture) =>
        ResolveCandidates(culture).Any(_documents.ContainsKey);

    /// <summary>Finds a template in cached JSON documents using the configured culture fallback order.</summary>
    /// <param name="key">The colon-separated JSON key.</param>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The matching string value, or <see langword="null"/> when no value is found.</returns>
    protected string? FindTemplate(string key, CultureInfo culture)
    {
        foreach (var candidate in ResolveCandidates(culture))
        {
            if (_documents.TryGetValue(candidate, out var root) && JsonStoreCore.TryGetValue(root, key, out var value))
                return value;
        }

        return null;
    }

    /// <inheritdoc />
    public virtual string? GetTemplate(string key, CultureInfo culture)
    {
        var result = FindTemplate(key, culture);
        if (result is null && Options.ThrowOnMissingStore && !HasCandidate(culture))
            throw new FileNotFoundException($"No translation files were found for culture '{culture.Name}' or fallback '{Options.FallbackCulture}'.");
        return result;
    }

    /// <inheritdoc />
    public virtual Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetTemplate(key, culture));
}

internal static class JsonStoreCore
{
    internal static List<string> ResolveCandidates(CultureInfo culture, string fallbackCulture, IDictionary<string, string> mappings)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return;
            var candidate = fileName!;
            if (seen.Add(candidate)) candidates.Add(candidate);
        }

        if (mappings.TryGetValue(culture.Name, out var mapped)) Add(mapped);
        Add($"{culture.Name}.json");
        Add(culture.TwoLetterISOLanguageName + ".json");

        if (!culture.Name.Equals(fallbackCulture, StringComparison.OrdinalIgnoreCase))
        {
            if (mappings.TryGetValue(fallbackCulture, out var fallbackMapped)) Add(fallbackMapped);
            var fallback = CultureInfo.GetCultureInfo(fallbackCulture);
            Add(fallback.Name + ".json");
            Add(fallback.TwoLetterISOLanguageName + ".json");
        }

        return candidates;
    }

    internal static string GetCacheKey(string resourceName)
    {
        if (resourceName.Contains(Path.DirectorySeparatorChar) || resourceName.Contains(Path.AltDirectorySeparatorChar))
            return Path.GetFileName(resourceName);
        var parts = resourceName.Split('.');
        return parts.Length >= 2 ? $"{parts[^2]}.json" : resourceName;
    }

    internal static bool TryGetValue(JsonElement root, string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(key)) return false;

        var current = root;
        foreach (var segment in key.Split([':'], StringSplitOptions.RemoveEmptyEntries)
                                   .Select(static item => item.Trim())
                                   .Where(static item => item.Length > 0))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return false;
        }

        if (current.ValueKind != JsonValueKind.String) return false;
        value = current.GetString() ?? string.Empty;
        return true;
    }
}
