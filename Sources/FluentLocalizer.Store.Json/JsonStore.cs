using FluentLocalizer.Core;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace FluentLocalizer.Store.Json;

/// <summary>
/// Provides translations stored in JSON files.
/// </summary>
public sealed class JsonStore : ITranslationStore, IDisposable
{
    private readonly JsonStoreOptions _options;
    private readonly Assembly _resourceAssembly;

    private readonly ConcurrentDictionary<string, JsonElement> _cache = new(StringComparer.OrdinalIgnoreCase);

    private FileSystemWatcher? _watcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonStore"/> class.
    /// </summary>
    /// <param name="options">The configuration options used to discover and load translation files.</param>
    public JsonStore(JsonStoreOptions? options = null)
    {
        _options = options ?? new JsonStoreOptions();
        _resourceAssembly = _options.ResourceAssembly ?? Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();

        LoadAllFiles();

        if (_options.ReloadOnChange && _options.SearchMode == JsonStoreLocation.FileSystem)
        {
            StartWatcher();
        }
    }

    /// <summary>
    /// Retrieves a translation template for the specified key and culture.
    /// </summary>
    /// <param name="key">The translation key to resolve.</param>
    /// <param name="culture">The culture used to select the translation template.</param>
    /// <returns>The matching template, or <c>null</c> when no template exists.</returns>
    public string? GetTemplate(string key, CultureInfo culture)
    {
        bool foundCandidate = false;

        foreach (var file in ResolveCandidates(culture))
        {
            if (_cache.TryGetValue(file, out var document))
            {
                foundCandidate = true;

                if (TryGetValue(document, key, out var value))
                {
                    return value;
                }
            }
        }

        if (_options.ThrowOnError && !foundCandidate)
        {
            throw new FileNotFoundException($"No translation files were found for culture '{culture.Name}' or fallback '{_options.FallbackCulture}'.");
        }

        return null;
    }

    /// <inheritdoc />
    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) => Task.FromResult(GetTemplate(key, culture));

    private HashSet<string> ResolveCandidates(CultureInfo culture)
    {
        HashSet<string> added = new(StringComparer.OrdinalIgnoreCase);

        void Add(string file)
        {
            if (!string.IsNullOrWhiteSpace(file))
                added.Add(file);
        }

        if (_options.FileMappings.TryGetValue(culture.Name, out var mapped))
            Add(mapped);

        Add($"{culture.Name}.json");

        if (!string.IsNullOrWhiteSpace(culture.TwoLetterISOLanguageName))
            Add($"{culture.TwoLetterISOLanguageName}.json");

        if (!culture.Name.Equals(_options.FallbackCulture, StringComparison.OrdinalIgnoreCase))
        {
            if (_options.FileMappings.TryGetValue(_options.FallbackCulture, out var fallbackMapped))
                Add(fallbackMapped);

            CultureInfo fallback = CultureInfo.GetCultureInfo(_options.FallbackCulture);

            Add($"{fallback.Name}.json");
            Add($"{fallback.TwoLetterISOLanguageName}.json");
        }

        return added;
    }

    private void LoadAllFiles()
    {
        var files = EnumerateFiles();

        if (files.Length == 0 && _options.ThrowOnError)
        {
            throw new FileNotFoundException("No translation files were found.");
        }

        foreach (var file in files)
            LoadFile(file);
    }

    private string[] EnumerateFiles()
    {
        if (_options.SearchMode == JsonStoreLocation.FileSystem)
        {
            var path = GetFullResourcesPath();
            if (!Directory.Exists(path))
                return [];
            return Directory.GetFiles(path, "*.json");
        }

        if (_options.SearchMode == JsonStoreLocation.EmbeddedResources)
            return [.. _resourceAssembly.GetManifestResourceNames().Where(name => name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))];

        throw new InvalidOperationException($"Unsupported search mode '{_options.SearchMode}'.");
    }

    private void LoadFile(string filePath)
    {
        try
        {
            string json;

            if (_options.SearchMode == JsonStoreLocation.FileSystem)
            {
                json = File.ReadAllText(filePath);
            }
            else
            {
                using var stream = _resourceAssembly.GetManifestResourceStream(filePath)
                    ?? throw new InvalidOperationException($"Embedded resource '{filePath}' was not found.");
                using var reader = new StreamReader(stream);
                json = reader.ReadToEnd();
            }

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement.Clone();

            var fileName = GetCacheKey(filePath);

            _cache.AddOrUpdate(fileName, root, (_, _) => root);
        }
        catch (Exception ex) when (ex is IOException || ex is JsonException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
        {
            if (_options.ThrowOnError)
            {
                throw;
            }
        }
    }

    private static string GetCacheKey(string filePath)
    {
        if (Path.IsPathRooted(filePath) || filePath.Contains(Path.DirectorySeparatorChar) || filePath.Contains(Path.AltDirectorySeparatorChar))
            return Path.GetFileName(filePath);
        var parts = filePath.Split('.');
        return parts.Length >= 2 ? $"{parts[^2]}.json" : filePath;
    }

    private static bool TryGetValue(JsonElement root, string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        JsonElement current = root;
        var segments = key.Split([':'], StringSplitOptions.RemoveEmptyEntries)
                          .Select(static s => s.Trim())
                          .Where(static s => s.Length > 0)
                          .ToArray();

        foreach (var segment in segments)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return false;
            }
        }

        if (current.ValueKind == JsonValueKind.String)
        {
            value = current.GetString() ?? string.Empty;
            return true;
        }

        return false;
    }

    private void RemoveFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        _cache.TryRemove(fileName, out _);
    }

    private void StartWatcher()
    {
        var path = GetFullResourcesPath();

        Directory.CreateDirectory(path);

        _watcher = new FileSystemWatcher(path, "*.json")
        {
            NotifyFilter =
                NotifyFilters.FileName |
                NotifyFilters.LastWrite |
                NotifyFilters.CreationTime
        };

        _watcher.Changed += (_, e) => LoadFile(e.FullPath);
        _watcher.Created += (_, e) => LoadFile(e.FullPath);
        _watcher.Renamed += (_, e) =>
        {
            RemoveFile(e.OldFullPath);
            LoadFile(e.FullPath);
        };
        _watcher.Deleted += (_, e) => RemoveFile(e.FullPath);

        _watcher.EnableRaisingEvents = true;
    }

    private string GetFullResourcesPath()
    {
        if (Path.IsPathRooted(_options.ResourcesPath))
        {
            return _options.ResourcesPath;
        }

        return Path.Combine(
            AppContext.BaseDirectory,
            _options.ResourcesPath);
    }

    /// <summary>
    /// Releases resources used by the store and its file watcher.
    /// </summary>
    public void Dispose() => _watcher?.Dispose();
}