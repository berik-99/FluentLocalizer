using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace FluentLocalizer.Store.Json;

/// <summary>Loads and caches culture JSON files embedded in an assembly.</summary>
public sealed class EmbeddedJsonStore : JsonTranslationStoreBase
{
    private readonly Assembly _assembly;
    private readonly Dictionary<string, Resource> _resources = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _namespaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _namespaceCultures = new(StringComparer.OrdinalIgnoreCase);
    // Resources to search per culture and namespace, in lookup order. Only namespaces that have a file get an entry.
    private readonly ConcurrentDictionary<(string Culture, string Namespace), Candidate[]> _candidates = new();

    private readonly record struct Candidate(Resource Resource, bool IsNamespaceFile);

    private sealed class Resource(string name)
    {
        internal readonly string Name = name;
        internal JsonElement? Root;
        internal bool Unreadable;
    }

    /// <summary>Creates a store for JSON resources embedded in the configured assembly.</summary>
    public EmbeddedJsonStore(EmbeddedJsonStoreOptions? options = null) : base(options ?? new EmbeddedJsonStoreOptions())
    {
        var storeOptions = (EmbeddedJsonStoreOptions)Options;
        if (storeOptions.MaxDocumentBytes < 0) throw new ArgumentOutOfRangeException(nameof(options), "MaxDocumentBytes cannot be negative.");
        _assembly = storeOptions.ResourceAssembly ?? Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var folder = storeOptions.ResourcesPath.Trim('.', '/', '\\').Replace('/', '.').Replace('\\', '.');
        var resourceNames = _assembly.GetManifestResourceNames()
            .Where(name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && (string.IsNullOrEmpty(folder) ||
                name.StartsWith(folder + ".", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("." + folder + ".", StringComparison.OrdinalIgnoreCase)));

        var resources = new List<Resource>();
        var aliases = new List<(string Key, Resource Resource)>();
        foreach (var resourceName in resourceNames)
        {
            using (var stream = _assembly.GetManifestResourceStream(resourceName))
            {
                if (stream is null) continue;
                if (stream.CanSeek) EnsureSize(stream.Length, resourceName);
            }

            var resource = new Resource(resourceName);
            resources.Add(resource);
            var key = JsonStoreCore.GetCacheKey(resourceName);
            var namespaceKey = JsonStoreCore.GetNamespaceCacheKey(resourceName, folder);
            if (namespaceKey is null)
            {
                _resources[key] = resource;
            }
            else
            {
                // Before 1.1.0 these files were keyed by their last segment; keep that name when no culture file uses it.
                _resources[namespaceKey] = resource;
                aliases.Add((key, resource));
                var separator = namespaceKey.IndexOf('.');
                _namespaceCultures.Add(namespaceKey[..separator]);
                _namespaces.Add(namespaceKey[(separator + 1)..^5]);
            }
        }
        foreach (var alias in aliases)
        {
            if (!_resources.ContainsKey(alias.Key)) _resources[alias.Key] = alias.Resource;
        }

        if (resources.Count == 0 && storeOptions.ThrowOnMissingStore)
            throw new FileNotFoundException($"No embedded translation JSON files were found in '{_assembly.FullName}' under '{storeOptions.ResourcesPath}'.");
        if (!storeOptions.LoadOnDemand)
        {
            foreach (var resource in resources) Load(resource);
        }
    }

    /// <inheritdoc />
    protected override bool HasCandidate(CultureInfo culture)
    {
        if (Candidates(culture, string.Empty).Length > 0) return true;
        if (_namespaceCultures.Count == 0) return false;
        var fallback = CultureInfo.GetCultureInfo(Options.FallbackCulture);
        return _namespaceCultures.Contains(culture.Name) || _namespaceCultures.Contains(culture.TwoLetterISOLanguageName) ||
            _namespaceCultures.Contains(fallback.Name) || _namespaceCultures.Contains(fallback.TwoLetterISOLanguageName);
    }

    /// <inheritdoc />
    protected override string? FindTemplate(string key, CultureInfo culture)
    {
        var rest = 0;
        var ns = _namespaces.Count == 0 ? null : JsonStoreCore.GetNamespace(key, out rest);
        if (ns is null || !_namespaces.Contains(ns))
        {
            ns = string.Empty;
            rest = 0;
        }

        foreach (var candidate in Candidates(culture, ns))
        {
            if (Load(candidate.Resource) is { } root &&
                (JsonStoreCore.TryGetValue(root, key, out var value) ||
                 candidate.IsNamespaceFile && JsonStoreCore.TryGetValue(root, key, rest, out value)))
            {
                return value;
            }
        }

        return null;
    }

    // Same order as the file and HTTP stores: for the culture, its neutral culture and then the fallback, the mapped
    // file, the namespace file and the culture file. Only embedded files are kept, so a lookup visits nothing else.
    private Candidate[] Candidates(CultureInfo culture, string ns)
    {
        var cacheKey = (culture.Name, ns);
        if (_candidates.TryGetValue(cacheKey, out var cached)) return cached;

        var candidates = new List<Candidate>();
        var seen = new HashSet<Resource>();
        void Add(string? fileName, bool isNamespaceFile)
        {
            if (fileName is not null && _resources.TryGetValue(fileName, out var resource) && seen.Add(resource))
                candidates.Add(new Candidate(resource, isNamespaceFile));
        }

        void AddCulture(CultureInfo candidateCulture)
        {
            if (Options.FileMappings.TryGetValue(candidateCulture.Name, out var mapped))
                Add(mapped, ns.Length > 0 && JsonStoreCore.IsNamespaceFileName(mapped, ns));
            foreach (var name in new[] { candidateCulture.Name, candidateCulture.TwoLetterISOLanguageName })
            {
                if (ns.Length > 0) Add($"{name}.{ns}.json", true);
                Add(name + ".json", false);
            }
        }

        AddCulture(culture);
        if (!culture.Name.Equals(Options.FallbackCulture, StringComparison.OrdinalIgnoreCase))
            AddCulture(CultureInfo.GetCultureInfo(Options.FallbackCulture));
        return _candidates.GetOrAdd(cacheKey, candidates.ToArray());
    }

    private JsonElement? Load(Resource resource)
    {
        lock (resource)
        {
            if (resource.Root is null && !resource.Unreadable)
            {
                try
                {
                    using var stream = _assembly.GetManifestResourceStream(resource.Name)
                        ?? throw new FileNotFoundException($"Translation resource '{resource.Name}' was not found.");
                    resource.Root = Parse(stream, resource.Name);
                }
                catch (Exception ex) when (!Options.ThrowOnMissingStore && (ex is IOException or JsonException or UnauthorizedAccessException))
                {
                    resource.Unreadable = true;
                }
            }

            return resource.Root;
        }
    }

    private JsonElement Parse(Stream stream, string resourceName)
    {
        if (!stream.CanSeek)
        {
            var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            stream = buffer;
        }
        EnsureSize(stream.Length, resourceName);

        // JsonDocument reads UTF-8 bytes directly and skips a UTF-8 BOM; StreamReader keeps UTF-16 and UTF-32 resources readable.
        if (HasUtf16OrUtf32Bom(stream))
        {
            using var reader = new StreamReader(stream);
            using var text = JsonDocument.Parse(reader.ReadToEnd());
            return text.RootElement.Clone();
        }

        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    private static bool HasUtf16OrUtf32Bom(Stream stream)
    {
        var bom = new byte[4];
        var read = stream.Read(bom, 0, bom.Length);
        stream.Position = 0;
        return read >= 2 && ((bom[0] == 0xFF && bom[1] == 0xFE) || (bom[0] == 0xFE && bom[1] == 0xFF)) ||
            read == 4 && bom[0] == 0 && bom[1] == 0 && bom[2] == 0xFE && bom[3] == 0xFF;
    }

    private void EnsureSize(long length, string resourceName)
    {
        if (Options.MaxDocumentBytes > 0 && length > Options.MaxDocumentBytes)
            throw new InvalidDataException($"Translation resource '{resourceName}' exceeds MaxDocumentBytes.");
    }
}
