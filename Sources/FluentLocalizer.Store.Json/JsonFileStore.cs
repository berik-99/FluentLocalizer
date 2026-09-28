using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<string, byte> _files = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;

    /// <summary>Creates a filesystem-backed JSON translation store.</summary>
    public JsonFileStore(JsonFileStoreOptions? options = null) : base(options ?? new JsonFileStoreOptions())
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Create("BROWSER")))
            throw new PlatformNotSupportedException("JsonFileStore is not supported in browser applications. Use EmbeddedJsonStore or HttpJsonStore instead.");

        _options = (JsonFileStoreOptions)Options;
        _path = Path.IsPathRooted(_options.ResourcesPath)
            ? _options.ResourcesPath
            : Path.Combine(AppContext.BaseDirectory, _options.ResourcesPath);
        LoadFiles();
        if (_options.ReloadOnChange) StartWatcher();
    }

    private void LoadFiles()
    {
        if (!Directory.Exists(_path))
        {
            if (Options.ThrowOnMissingStore) throw new FileNotFoundException($"Translation directory '{_path}' was not found.");
            return;
        }

        var files = Directory.GetFiles(_path, "*.json", SearchOption.AllDirectories);
        if (files.Length == 0 && Options.ThrowOnMissingStore)
            throw new FileNotFoundException($"No translation JSON files were found in '{_path}'.");
        foreach (var path in files)
        {
            _files[GetRelativePath(path)] = 0;
            if (Options.ThrowOnMissingStore)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
            }
        }
    }

    private void LoadFile(string path)
    {
        try
        {
            if (File.Exists(path)) _files[GetRelativePath(path)] = 0;
        }
        catch (Exception ex) when (!Options.ThrowOnMissingStore && ex is IOException or UnauthorizedAccessException)
        { }
    }

    private string GetRelativePath(string path)
    {
        var root = _path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
    }

    /// <inheritdoc />
    protected override bool HasCandidate(System.Globalization.CultureInfo culture)
    {
        var cultures = new List<System.Globalization.CultureInfo> { culture };
        var fallback = System.Globalization.CultureInfo.GetCultureInfo(Options.FallbackCulture);
        if (!culture.Name.Equals(fallback.Name, StringComparison.OrdinalIgnoreCase)) cultures.Add(fallback);

        foreach (var candidateCulture in cultures)
        {
            var names = new[] { candidateCulture.Name, candidateCulture.TwoLetterISOLanguageName };
            foreach (var name in names)
            {
                if (_files.Keys.Any(file => file.StartsWith(name + ".", StringComparison.OrdinalIgnoreCase) ||
                                            file.StartsWith(name + "/", StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            if (Options.FileMappings.TryGetValue(candidateCulture.Name, out var mapped) &&
                _files.ContainsKey(mapped.Replace(Path.DirectorySeparatorChar, '/')))
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    protected override string? FindTemplate(string key, System.Globalization.CultureInfo culture)
    {
        foreach (var candidate in JsonStoreCore.ResolveFileCandidates(culture, Options.FallbackCulture, Options.FileMappings, key))
        {
            var path = Path.Combine(_path, candidate.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) continue;

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                if (JsonStoreCore.TryGetValue(document.RootElement, key, out var value) ||
                    (JsonStoreCore.IsNamespaceFile(candidate, key) &&
                     JsonStoreCore.TryGetValue(document.RootElement, JsonStoreCore.GetLookupKey(key), out value)))
                    return value;
            }
            catch (Exception ex) when (!Options.ThrowOnMissingStore && ex is IOException or JsonException or UnauthorizedAccessException)
            { }
        }

        return null;
    }

    private void StartWatcher()
    {
        Directory.CreateDirectory(_path);
        _watcher = new FileSystemWatcher(_path, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            IncludeSubdirectories = true
        };
        _watcher.Changed += (_, e) => { LoadFile(e.FullPath); ClearTemplateCache(); };
        _watcher.Created += (_, e) => { LoadFile(e.FullPath); ClearTemplateCache(); };
        _watcher.Renamed += (sender, e) => { _files.TryRemove(GetRelativePath(e.OldFullPath), out var ignored); LoadFile(e.FullPath); ClearTemplateCache(); };
        _watcher.Deleted += (sender, e) => { _files.TryRemove(GetRelativePath(e.FullPath), out var ignored); ClearTemplateCache(); };
        _watcher.EnableRaisingEvents = true;
    }

    /// <inheritdoc />
    public void Dispose() => _watcher?.Dispose();
}
