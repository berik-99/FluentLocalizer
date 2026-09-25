using System.Globalization;
using System.Text.Json;

namespace FluentLocalizer.Store.Json;

/// <summary>Loads and caches culture JSON files from the local filesystem.</summary>
public sealed class JsonFileStore : JsonTranslationStoreBase, IDisposable
{
    private readonly JsonFileStoreOptions _options;
    private readonly string _path;
    private FileSystemWatcher? _watcher;

    /// <summary>Creates a filesystem-backed JSON translation store.</summary>
    public JsonFileStore(JsonFileStoreOptions? options = null) : base(options ?? new JsonFileStoreOptions())
    {
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

        var files = Directory.GetFiles(_path, "*.json");
        if (files.Length == 0 && Options.ThrowOnMissingStore)
            throw new FileNotFoundException($"No translation JSON files were found in '{_path}'.");
        foreach (var path in files) LoadFile(path);
    }

    private void LoadFile(string path)
    {
        try { SetDocument(Path.GetFileName(path), File.ReadAllText(path)); }
        catch (Exception ex) when (!Options.ThrowOnMissingStore && (ex is IOException or JsonException or UnauthorizedAccessException))
        { }
    }

    private void StartWatcher()
    {
        Directory.CreateDirectory(_path);
        _watcher = new FileSystemWatcher(_path, "*.json")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };
        _watcher.Changed += (_, e) => LoadFile(e.FullPath);
        _watcher.Created += (_, e) => LoadFile(e.FullPath);
        _watcher.Renamed += (_, e) => { RemoveDocument(Path.GetFileName(e.OldFullPath)); LoadFile(e.FullPath); };
        _watcher.Deleted += (_, e) => RemoveDocument(Path.GetFileName(e.FullPath));
        _watcher.EnableRaisingEvents = true;
    }

    /// <inheritdoc />
    public void Dispose() => _watcher?.Dispose();
}
