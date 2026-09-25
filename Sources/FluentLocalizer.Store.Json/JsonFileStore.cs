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

        var files = Directory.GetFiles(_path, "*.json");
        if (files.Length == 0 && Options.ThrowOnMissingStore)
            throw new FileNotFoundException($"No translation JSON files were found in '{_path}'.");
        foreach (var path in files) LoadFile(path);
    }

    private void LoadFile(string path)
    {
        try
        {
            string json;
            try { json = File.ReadAllText(path); }
            catch (IOException) when (!Options.ThrowOnMissingStore && File.Exists(path))
            {
                Thread.Sleep(50);
                json = File.ReadAllText(path);
            }

            SetDocument(Path.GetFileName(path), json);
        }
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
