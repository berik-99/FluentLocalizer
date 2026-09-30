using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;

namespace FluentLocalizer.Store.Json;

/// <summary>Loads and caches culture JSON files from the local filesystem.</summary>
#if NET8_0_OR_GREATER
[UnsupportedOSPlatform("browser")]
#endif
public sealed class JsonFileStore : JsonTranslationStoreBase, IDisposable
{
    private readonly JsonFileStoreOptions _options;
    private readonly string _path;
    private ConcurrentDictionary<string, byte> _files = new(StringComparer.OrdinalIgnoreCase);
    private string[] _indexedCultures = [];
    private ConcurrentDictionary<string, CacheSlot> _documentCache = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;

    private sealed class CacheSlot
    {
        internal JsonElement? Root;
    }

    /// <summary>Creates a filesystem-backed JSON translation store.</summary>
    public JsonFileStore(JsonFileStoreOptions? options = null) : base(options ?? new JsonFileStoreOptions())
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
            throw new PlatformNotSupportedException("JsonFileStore is not supported in browser applications. Use EmbeddedJsonStore or HttpJsonStore instead.");

        _options = (JsonFileStoreOptions)Options;
        if (_options.MaxDocumentBytes < 0) throw new ArgumentOutOfRangeException(nameof(options), "MaxDocumentBytes cannot be negative.");
        _path = Path.GetFullPath(Path.IsPathRooted(_options.ResourcesPath)
            ? _options.ResourcesPath
            : Path.Combine(AppContext.BaseDirectory, _options.ResourcesPath));
        foreach (var mapping in Options.FileMappings.Values)
            JsonStoreCore.ValidateRelativeJsonPath(mapping);
        LoadFiles();
        if (_options.ReloadOnChange) StartWatcher();
    }

    private void LoadFiles()
    {
        if (!Directory.Exists(_path))
        {
            Interlocked.Exchange(ref _files, new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));
            UpdateCultureIndex();
            if (Options.ThrowOnMissingStore) throw new FileNotFoundException($"Translation directory '{_path}' was not found.");
            return;
        }

        var files = Directory.GetFiles(_path, "*.json", SearchOption.AllDirectories);
        if (files.Length == 0 && Options.ThrowOnMissingStore)
            throw new FileNotFoundException($"No translation JSON files were found in '{_path}'.");
        var indexed = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in files)
        {
            indexed[GetRelativePath(path)] = 0;
            if (Options.ThrowOnMissingStore)
            {
                EnsureNoLinks(path);
                using var document = JsonDocument.Parse(ReadJson(path));
            }
        }
        Interlocked.Exchange(ref _files, indexed);
        UpdateCultureIndex();
    }

    private void LoadFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                if (Volatile.Read(ref _files).TryAdd(GetRelativePath(path), 0)) UpdateCultureIndex();
            }
        }
        catch (Exception ex) when (!Options.ThrowOnMissingStore && ex is IOException or UnauthorizedAccessException)
        { }
    }

    private string GetRelativePath(string path)
    {
        var root = _path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return path[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    /// <inheritdoc />
    protected override bool HasCandidate(System.Globalization.CultureInfo culture)
    {
        var available = AvailableCultures();
        return JsonStoreCore.SelectCulture(culture, available) is not null ||
            JsonStoreCore.SelectCulture(System.Globalization.CultureInfo.GetCultureInfo(Options.FallbackCulture), available) is not null;
    }

    private string[] AvailableCultures()
    {
        var indexed = Volatile.Read(ref _indexedCultures);
        if (Options.FileMappings.Count == 0) return indexed;
        var files = Volatile.Read(ref _files);
        return [.. indexed
            .Concat(Options.FileMappings.Where(mapping => files.ContainsKey(mapping.Value.Replace('\\', '/'))).Select(static mapping => mapping.Key))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private void UpdateCultureIndex() => Volatile.Write(ref _indexedCultures,
        [.. Volatile.Read(ref _files).Keys.Select(JsonStoreCore.CultureFromPath)
            .Where(static name => name is not null).Select(static name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)]);

    /// <inheritdoc />
    protected override string? FindTemplate(string key, System.Globalization.CultureInfo culture)
    {
        var cache = Volatile.Read(ref _documentCache);
        var available = AvailableCultures();
        var selected = JsonStoreCore.SelectCulture(culture, available);
        var fallback = JsonStoreCore.SelectCulture(System.Globalization.CultureInfo.GetCultureInfo(Options.FallbackCulture), available);
        var candidates = new List<string>();
        if (selected is not null)
            candidates.AddRange(JsonStoreCore.ResolveFileCandidates(System.Globalization.CultureInfo.GetCultureInfo(selected), selected, Options.FileMappings, key));
        if (fallback?.Equals(selected, StringComparison.OrdinalIgnoreCase) == false)
            candidates.AddRange(JsonStoreCore.ResolveFileCandidates(System.Globalization.CultureInfo.GetCultureInfo(fallback), fallback, Options.FileMappings, key));
        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var cacheKey = candidate.Replace('\\', '/');
            var path = Path.GetFullPath(Path.Combine(_path, JsonStoreCore.ValidateRelativeJsonPath(candidate).Replace('/', Path.DirectorySeparatorChar)));
            if (!path.StartsWith(_path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new System.Security.SecurityException("Translation path escapes ResourcesPath.");
            try
            {
                if (!cache.TryGetValue(cacheKey, out var slot))
                {
                    if (!File.Exists(path)) continue;
                    slot = cache.GetOrAdd(cacheKey, static _ => new CacheSlot());
                }
                JsonElement root;
                lock (slot)
                {
                    if (slot.Root is JsonElement cached)
                    {
                        root = cached;
                    }
                    else
                    {
                        if (!File.Exists(path)) continue;
                        EnsureNoLinks(path);
                        using var document = JsonDocument.Parse(ReadJson(path));
                        root = document.RootElement.Clone();
                        slot.Root = root;
                    }
                }
                if (JsonStoreCore.TryGetValue(root, key, out var value)
                    || (JsonStoreCore.IsNamespaceFile(candidate, key)
                    && JsonStoreCore.TryGetValue(root, JsonStoreCore.GetLookupKey(key), out value)))
                {
                    return value;
                }
            }
            catch (Exception ex) when (!Options.ThrowOnMissingStore && ex is IOException or JsonException or UnauthorizedAccessException)
            { }
        }

        return null;
    }

    private void EnsureNoLinks(string path)
    {
        var current = _path;
        if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            throw new System.Security.SecurityException($"Translation path '{current}' is a symbolic link or reparse point.");
        foreach (var segment in GetRelativePath(path).Split('/'))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new System.Security.SecurityException($"Translation path '{current}' is a symbolic link or reparse point.");
        }
    }

    private string ReadJson(string path)
    {
        var limit = _options.MaxDocumentBytes;
        if (limit == 0) return File.ReadAllText(path);
        using var stream = File.OpenRead(path);
        if (stream.Length > limit) throw new InvalidDataException($"Translation file '{path}' exceeds MaxDocumentBytes.");
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > limit) throw new InvalidDataException($"Translation file '{path}' exceeds MaxDocumentBytes.");
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        using var reader = new StreamReader(buffer, System.Text.Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    /// <inheritdoc />
    public override string? GetTemplate(string key, System.Globalization.CultureInfo culture)
    {
        var result = FindTemplate(key, culture);
        if (result is null && Options.ThrowOnMissingStore && !HasCandidate(culture))
            throw new FileNotFoundException($"No translation files were found for culture '{culture.Name}' or fallback '{Options.FallbackCulture}'.");
        return result;
    }

    private void InvalidateCache() =>
        Interlocked.Exchange(ref _documentCache, new ConcurrentDictionary<string, CacheSlot>(StringComparer.OrdinalIgnoreCase));

    private void InvalidateFile(string path) => Volatile.Read(ref _documentCache).TryRemove(GetRelativePath(path), out _);

    private void StartWatcher()
    {
        Directory.CreateDirectory(_path);
        _watcher = new FileSystemWatcher(_path, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = true
        };
        _watcher.Changed += (_, e) => { LoadFile(e.FullPath); InvalidateFile(e.FullPath); };
        _watcher.Created += (_, e) => { LoadFile(e.FullPath); InvalidateFile(e.FullPath); };
        _watcher.Renamed += (sender, e) => { Volatile.Read(ref _files).TryRemove(GetRelativePath(e.OldFullPath), out _); LoadFile(e.FullPath); UpdateCultureIndex(); InvalidateFile(e.OldFullPath); InvalidateFile(e.FullPath); };
        _watcher.Deleted += (sender, e) => { Volatile.Read(ref _files).TryRemove(GetRelativePath(e.FullPath), out _); UpdateCultureIndex(); InvalidateFile(e.FullPath); };
        _watcher.Error += (_, _) =>
        {
            try { LoadFiles(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) { }
            InvalidateCache();
        };
        _watcher.EnableRaisingEvents = true;
    }

    /// <inheritdoc />
    public void Dispose() => _watcher?.Dispose();
}
