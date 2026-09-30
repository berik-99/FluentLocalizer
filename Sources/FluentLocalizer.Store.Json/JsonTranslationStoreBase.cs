using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace FluentLocalizer.Store.Json;

/// <summary>Base class for JSON stores that share culture fallback and nested-key lookup.</summary>
public abstract class JsonTranslationStoreBase(JsonStoreSettings options) : ITranslationStore
{
    private readonly ConcurrentDictionary<string, JsonElement> _documents = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _templateCacheLock = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, string Value)>> _templateCache = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, string Value)> _templateCacheOrder = new();
    // ponytail: cap retained strings at 512 entries; tune only if real workloads need a different bound.
    private const int TemplateCacheCapacity = 512;

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
    protected virtual bool HasCandidate(CultureInfo culture) =>
        ResolveCandidates(culture).Any(_documents.ContainsKey);

    /// <summary>Finds a template in cached JSON documents using the configured culture fallback order.</summary>
    /// <param name="key">The colon-separated JSON key.</param>
    /// <param name="culture">The requested culture.</param>
    /// <returns>The matching string value, or <see langword="null"/> when no value is found.</returns>
    protected virtual string? FindTemplate(string key, CultureInfo culture)
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
        var cacheKey = culture.Name + "\0" + key;
        lock (_templateCacheLock)
        {
            if (_templateCache.TryGetValue(cacheKey, out var cached))
            {
                _templateCacheOrder.Remove(cached);
                _templateCacheOrder.AddFirst(cached);
                return cached.Value.Value;
            }
        }

        var result = FindTemplate(key, culture);
        if (result is null && Options.ThrowOnMissingStore && !HasCandidate(culture))
            throw new FileNotFoundException($"No translation files were found for culture '{culture.Name}' or fallback '{Options.FallbackCulture}'.");

        if (result is not null)
        {
            lock (_templateCacheLock)
            {
                if (_templateCache.TryGetValue(cacheKey, out var existing))
                {
                    _templateCacheOrder.Remove(existing);
                    _templateCacheOrder.AddFirst(existing);
                }
                else
                {
                    var node = _templateCacheOrder.AddFirst((cacheKey, result));
                    _templateCache.Add(cacheKey, node);
                    if (_templateCache.Count > TemplateCacheCapacity)
                    {
                        var oldest = _templateCacheOrder.Last!;
                        _templateCacheOrder.RemoveLast();
                        _templateCache.Remove(oldest.Value.Key);
                    }
                }
            }
        }

        return result;
    }

    /// <summary>Clears cached resolved templates after the underlying store changes.</summary>
    protected void ClearTemplateCache()
    {
        lock (_templateCacheLock)
        {
            _templateCache.Clear();
            _templateCacheOrder.Clear();
        }
    }

    /// <inheritdoc />
    public virtual Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetTemplate(key, culture));
}

internal static class JsonStoreCore
{
    internal static string? CultureFromPath(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        if (parts.Length == 2 && TryCulture(parts[0], out var folderCulture)) return folderCulture;
        if (parts.Length != 1 || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) return null;

        var stem = parts[0][..^5];
        if (TryCulture(stem, out var wholeCulture)) return wholeCulture;
        var separator = stem.IndexOf('.');
        if (separator < 0) return null;
        if (TryCulture(stem[..separator], out var prefixCulture)) return prefixCulture;
        return TryCulture(stem[(stem.LastIndexOf('.') + 1)..], out var suffixCulture) ? suffixCulture : null;
    }

    private static bool TryCulture(string name, out string culture)
    {
        culture = string.Empty;
        try
        {
            culture = CultureInfo.GetCultureInfo(name).Name;
            return culture.Length > 0 && culture.Equals(name, StringComparison.OrdinalIgnoreCase);
        }
        catch (CultureNotFoundException) { return false; }
    }

    internal static string? SelectCulture(CultureInfo requested, IEnumerable<string> available)
    {
        var names = available.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Contains(requested.Name, StringComparer.OrdinalIgnoreCase)) return requested.Name;

        if (requested.IsNeutralCulture)
        {
            return names.Where(name => name.StartsWith(requested.Name + "-", StringComparison.OrdinalIgnoreCase))
                .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
        }

        var neutral = requested.Parent.Name;
        if (names.Contains(neutral, StringComparer.OrdinalIgnoreCase)) return neutral;
        if (names.Any(name => name.StartsWith(neutral + "-", StringComparison.OrdinalIgnoreCase)))
            throw new FileNotFoundException($"No translation files were found for culture '{requested.Name}'; another regional variant exists.");
        return null;
    }

    internal static string ValidateRelativeJsonPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.IndexOfAny(['?', '#', '\0']) >= 0)
            throw new ArgumentException("Translation paths must be relative JSON paths.", nameof(path));

        var normalized = path.Replace('\\', '/');
        if (normalized.StartsWith('/')
            || normalized.Split('/').Any(static segment => segment is "" or "." or ".." || segment.Contains(':'))
            || !normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Translation paths must stay inside the configured resource path and end in .json.", nameof(path));
        }

        return normalized;
    }

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

    internal static List<string> ResolveFileCandidates(CultureInfo culture, string fallbackCulture, IDictionary<string, string> mappings, string key)
    {
        var segments = key.Split(':').Select(static segment => segment.Trim()).Where(static segment => segment.Length > 0).ToArray();
        var namespaced = HasNamespace(segments);
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path) && seen.Add(path!)) candidates.Add(path!);
        }

        void AddCulture(CultureInfo candidateCulture)
        {
            if (mappings.TryGetValue(candidateCulture.Name, out var mapped)) Add(mapped);
            if (namespaced)
            {
                Add($"{candidateCulture.Name}.{segments[0]}.json");
                Add($"{segments[0]}.{candidateCulture.Name}.json");
                Add(Path.Combine(candidateCulture.Name, segments[0] + ".json"));
            }
            Add(candidateCulture.Name + ".json");
            Add(Path.Combine(candidateCulture.Name, candidateCulture.Name + ".json"));
            if (!candidateCulture.Name.Equals(candidateCulture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
            {
                if (namespaced)
                {
                    Add($"{candidateCulture.TwoLetterISOLanguageName}.{segments[0]}.json");
                    Add($"{segments[0]}.{candidateCulture.TwoLetterISOLanguageName}.json");
                    Add(Path.Combine(candidateCulture.TwoLetterISOLanguageName, segments[0] + ".json"));
                }
                Add(candidateCulture.TwoLetterISOLanguageName + ".json");
                Add(Path.Combine(candidateCulture.TwoLetterISOLanguageName, candidateCulture.TwoLetterISOLanguageName + ".json"));
            }
        }

        AddCulture(culture);
        if (!culture.Name.Equals(fallbackCulture, StringComparison.OrdinalIgnoreCase))
            AddCulture(CultureInfo.GetCultureInfo(fallbackCulture));

        return candidates;
    }

    internal static string GetLookupKey(string key)
    {
        var segments = key.Split(':').Select(static segment => segment.Trim()).Where(static segment => segment.Length > 0).ToArray();
        return HasNamespace(segments)
            ? string.Join(":", segments.Skip(1))
            : key;
    }

    internal static bool IsNamespaceFile(string candidate, string key)
    {
        var segments = key.Split(':').Select(static segment => segment.Trim()).Where(static segment => segment.Length > 0).ToArray();
        if (!HasNamespace(segments))
            return false;

        var normalized = candidate.Replace('\\', '/');
        return normalized.StartsWith(segments[0] + ".", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("." + segments[0] + ".json", StringComparison.OrdinalIgnoreCase) ||
               normalized.EndsWith("/" + segments[0] + ".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasNamespace(string[] segments) =>
        segments.Length > 1 && segments[0].Length > 0 && segments[0] is not "." and not ".." &&
        segments[0].IndexOfAny(['/', '\\', '?', '#', '\0', '*', '"', '<', '>', '|']) < 0;

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
