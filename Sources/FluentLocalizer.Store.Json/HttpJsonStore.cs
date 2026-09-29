using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FluentLocalizer.Store.Json;

/// <summary>Loads culture JSON files over HTTP and caches them for synchronous lookups after loading.</summary>
/// <remarks>The supplied <see cref="HttpClient"/> remains owned by the caller.</remarks>
public sealed class HttpJsonStore : ITranslationStore, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly HttpJsonStoreOptions _options;
    private readonly ConcurrentDictionary<string, CultureCache> _cultures = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _manifestGate = new(1, 1);
    private ManifestCatalog? _manifest;
    private bool _manifestLoaded;
    private string? _manifestEtag;
    private DateTimeOffset? _manifestLastModified;

    private sealed record CachedFile(JsonElement Root, string? ETag, DateTimeOffset? LastModified);

    private sealed class ManifestCatalog(string[] files)
    {
        internal readonly string[] Files = files;
        internal readonly HashSet<string> FileSet = new(files, StringComparer.OrdinalIgnoreCase);
        internal readonly string[] Cultures = files.Select(JsonStoreCore.CultureFromPath)
            .Where(static name => name is not null).Select(static name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private sealed class CultureCache
    {
        internal readonly SemaphoreSlim Gate = new(1, 1);
        internal Dictionary<string, CachedFile>? Documents;
        internal HashSet<string> Attempted = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Creates an HTTP JSON store. The client remains owned by the caller.</summary>
    public HttpJsonStore(HttpClient httpClient, HttpJsonStoreOptions? options = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? new HttpJsonStoreOptions();
        ValidateResourcePath(_options.ResourcesPath);
        foreach (var mapping in _options.FileMappings.Values)
            ValidateHttpFileName(mapping);
    }

    /// <summary>Preloads a culture and its fallback, including namespace files listed in manifest.json when present.</summary>
    public async Task LoadAsync(CultureInfo culture, CancellationToken cancellationToken = default)
    {
        if (culture is null) throw new ArgumentNullException(nameof(culture));
        await LoadCultureAsync(culture, null, cancellationToken).ConfigureAwait(false);
        if (!culture.Name.Equals(_options.FallbackCulture, StringComparison.OrdinalIgnoreCase))
            await LoadCultureAsync(CultureInfo.GetCultureInfo(_options.FallbackCulture), null, cancellationToken).ConfigureAwait(false);
        EnsureAvailable(culture);
    }

    /// <summary>Preloads the requested cultures and fallback, including namespace files listed in manifest.json when present.</summary>
    public async Task LoadAsync(IEnumerable<string> cultures, CancellationToken cancellationToken = default)
    {
        if (cultures is null) throw new ArgumentNullException(nameof(cultures));
        var requested = cultures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var name in requested)
            await LoadCultureAsync(CultureInfo.GetCultureInfo(name), null, cancellationToken).ConfigureAwait(false);
        await LoadCultureAsync(CultureInfo.GetCultureInfo(_options.FallbackCulture), null, cancellationToken).ConfigureAwait(false);
        EnsureAvailable(CultureInfo.GetCultureInfo(_options.FallbackCulture));
        foreach (var name in requested)
            EnsureAvailable(CultureInfo.GetCultureInfo(name));
    }

    /// <summary>Refreshes loaded cultures atomically. An invalid response leaves the previous culture snapshot available.</summary>
    public async Task RefreshStoreAsync(CancellationToken cancellationToken = default)
    {
        await LoadManifestAsync(cancellationToken, refresh: true).ConfigureAwait(false);
        foreach (var name in _cultures.Keys.ToArray())
            await LoadCultureAsync(CultureInfo.GetCultureInfo(name), null, cancellationToken, forceRefresh: true).ConfigureAwait(false);
        foreach (var name in _cultures.Keys)
            EnsureAvailable(CultureInfo.GetCultureInfo(name));
    }

    /// <inheritdoc />
    public async Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
    {
        if (culture is null) throw new ArgumentNullException(nameof(culture));
        await LoadCultureAsync(culture, key, cancellationToken).ConfigureAwait(false);
        var value = FindTemplate(key, culture);
        if (value is not null || culture.Name.Equals(_options.FallbackCulture, StringComparison.OrdinalIgnoreCase))
        {
            EnsureAvailable(culture);
            return value;
        }
        var fallback = CultureInfo.GetCultureInfo(_options.FallbackCulture);
        await LoadCultureAsync(fallback, key, cancellationToken).ConfigureAwait(false);
        EnsureAvailable(culture);
        return FindTemplate(key, fallback);
    }

    /// <inheritdoc />
    public string? GetTemplate(string key, CultureInfo culture)
    {
        EnsureLoaded(culture);
        var value = FindTemplate(key, culture);
        if (value is not null || culture.Name.Equals(_options.FallbackCulture, StringComparison.OrdinalIgnoreCase))
            return value;
        var fallback = CultureInfo.GetCultureInfo(_options.FallbackCulture);
        EnsureLoaded(fallback);
        EnsureAvailable(culture);
        return FindTemplate(key, fallback);
    }

    private async Task LoadCultureAsync(CultureInfo culture, string? key, CancellationToken cancellationToken, bool forceRefresh = false)
    {
        await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        var state = _cultures.GetOrAdd(culture.Name, static _ => new CultureCache());
        var candidates = Candidates(culture, key);
        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var current = Volatile.Read(ref state.Documents);
            if (!forceRefresh && current is not null && candidates.All(state.Attempted.Contains)) return;
            var manifest = Volatile.Read(ref _manifest);
            var search = forceRefresh
                ? state.Attempted.Union(candidates, StringComparer.OrdinalIgnoreCase)
                    .Where(file => manifest is null || manifest.FileSet.Contains(file))
                : candidates;

            var next = forceRefresh ? new Dictionary<string, CachedFile>(StringComparer.OrdinalIgnoreCase) :
                current is null ? new Dictionary<string, CachedFile>(StringComparer.OrdinalIgnoreCase) :
                new Dictionary<string, CachedFile>(current, StringComparer.OrdinalIgnoreCase);

            var attempted = new List<string>();
            foreach (var fileName in search)
            {
                if (!forceRefresh && state.Attempted.Contains(fileName))
                {
                    if (key is not null && next.TryGetValue(fileName, out var cached) && HasValue(cached.Root, fileName, key)) break;
                    continue;
                }
                attempted.Add(fileName);
                using var request = new HttpRequestMessage(HttpMethod.Get, GetRequestUri(fileName));
                if (forceRefresh)
                {
                    request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                    if (current is not null && current.TryGetValue(fileName, out var previous))
                    {
                        if (previous.ETag is not null) request.Headers.IfNoneMatch.ParseAdd(previous.ETag);
                        if (previous.LastModified is not null) request.Headers.IfModifiedSince = previous.LastModified;
                    }
                }
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound) continue;
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    if (current is null || !current.TryGetValue(fileName, out var previous))
                        throw new HttpRequestException($"Unexpected 304 response for '{fileName}'.");
                    next[fileName] = previous;
                    continue;
                }
                response.EnsureSuccessStatusCode();
#if NET8_0_OR_GREATER
                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                next[fileName] = new CachedFile(document.RootElement.Clone(), response.Headers.ETag?.ToString(), response.Content.Headers.LastModified);
                if (!forceRefresh && key is not null && HasValue(next[fileName].Root, fileName, key))
                    break;
            }

            state.Attempted = forceRefresh ? new HashSet<string>(attempted, StringComparer.OrdinalIgnoreCase) :
                new HashSet<string>(state.Attempted.Concat(attempted), StringComparer.OrdinalIgnoreCase);
            Volatile.Write(ref state.Documents, next);
        }
        finally
        {
            state.Gate.Release();
        }
    }

    private async Task LoadManifestAsync(CancellationToken cancellationToken, bool refresh = false)
    {
        if (!refresh && Volatile.Read(ref _manifestLoaded)) return;
        await _manifestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!refresh && _manifestLoaded) return;
            using var request = new HttpRequestMessage(HttpMethod.Get, GetRequestUri("manifest.json"));
            if (refresh)
            {
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                if (_manifestEtag is not null) request.Headers.IfNoneMatch.ParseAdd(_manifestEtag);
                if (_manifestLastModified is not null) request.Headers.IfModifiedSince = _manifestLastModified;
            }
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                if (!_manifestLoaded) throw new HttpRequestException("Unexpected 304 response for manifest.json.");
                return;
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                Volatile.Write(ref _manifest, null);
            }
            else
            {
                response.EnsureSuccessStatusCode();
#if NET8_0_OR_GREATER
                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    throw new JsonException("manifest.json must contain an array of relative JSON paths.");
                var files = document.RootElement.EnumerateArray().Select(entry =>
                    entry.ValueKind == JsonValueKind.String ? ValidateHttpFileName(entry.GetString()!) :
                    throw new JsonException("manifest.json entries must be JSON file paths.")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                Volatile.Write(ref _manifest, new ManifestCatalog(files));
            }
            _manifestEtag = response.Headers.ETag?.ToString();
            _manifestLastModified = response.Content?.Headers.LastModified;
            Volatile.Write(ref _manifestLoaded, true);
        }
        finally { _manifestGate.Release(); }
    }

    private IReadOnlyList<string> Candidates(CultureInfo culture, string? key)
    {
        var manifest = Volatile.Read(ref _manifest);
        if (manifest is null)
            return key is null ? JsonStoreCore.ResolveCandidates(culture, culture.Name, _options.FileMappings)
                : JsonStoreCore.ResolveFileCandidates(culture, culture.Name, _options.FileMappings, key);

        var available = manifest.Cultures
            .Concat(_options.FileMappings.Where(mapping => manifest.FileSet.Contains(mapping.Value.Replace('\\', '/')))
                .Select(static mapping => mapping.Key))
            .ToArray();
        var selected = JsonStoreCore.SelectCulture(culture, available);
        if (selected is null) return [];
        if (key is null)
            return manifest.Files.Where(file => JsonStoreCore.CultureFromPath(file)?.Equals(selected, StringComparison.OrdinalIgnoreCase) == true ||
                _options.FileMappings.TryGetValue(selected, out var mapped) && file.Equals(mapped.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase).ToArray();
        return JsonStoreCore.ResolveFileCandidates(CultureInfo.GetCultureInfo(selected), selected, _options.FileMappings, key)
            .Select(static candidate => candidate.Replace('\\', '/'))
            .Where(manifest.FileSet.Contains).ToArray();
    }

    private static bool HasValue(JsonElement root, string fileName, string key) =>
        JsonStoreCore.TryGetValue(root, key, out _) ||
        (JsonStoreCore.IsNamespaceFile(fileName, key) &&
         JsonStoreCore.TryGetValue(root, JsonStoreCore.GetLookupKey(key), out _));

    private string? FindTemplate(string key, CultureInfo culture)
    {
        if (!_cultures.TryGetValue(culture.Name, out var state)) return null;
        var documents = Volatile.Read(ref state.Documents);
        if (documents is null) return null;
        foreach (var fileName in Candidates(culture, key))
        {
            if (!documents.TryGetValue(fileName, out var root)) continue;
            if (JsonStoreCore.TryGetValue(root.Root, key, out var value) ||
                (JsonStoreCore.IsNamespaceFile(fileName, key) &&
                 JsonStoreCore.TryGetValue(root.Root, JsonStoreCore.GetLookupKey(key), out value)))
                return value;
        }
        return null;
    }

    private void EnsureLoaded(CultureInfo culture)
    {
        if (!_cultures.TryGetValue(culture.Name, out var state) || Volatile.Read(ref state.Documents) is null)
            throw new InvalidOperationException($"Translation files for culture '{culture.Name}' have not been loaded. Call LoadAsync before synchronous resolution, or use ResolveAsync.");
    }

    private void EnsureAvailable(CultureInfo culture)
    {
        var fallback = CultureInfo.GetCultureInfo(_options.FallbackCulture);
        if (Volatile.Read(ref _manifest) is null && !culture.IsNeutralCulture &&
            !culture.Name.Equals(fallback.Name, StringComparison.OrdinalIgnoreCase) &&
            culture.Parent.Name.Equals(fallback.Parent.Name, StringComparison.OrdinalIgnoreCase) &&
            !HasDocuments(culture))
            throw new FileNotFoundException($"No translation files were found for culture '{culture.Name}'; only another regional variant is available.");
        if (!_options.ThrowOnMissingStore) return;
        if (HasDocuments(culture) || HasDocuments(fallback)) return;
        throw new FileNotFoundException($"No translation files were found for culture '{culture.Name}' or fallback '{fallback.Name}'.");
    }

    private bool HasDocuments(CultureInfo culture) =>
        _cultures.TryGetValue(culture.Name, out var state) && Volatile.Read(ref state.Documents)?.Count > 0;

    private string GetRequestUri(string fileName)
    {
        fileName = ValidateHttpFileName(fileName);
        ValidateResourcePath(_options.ResourcesPath);
        var path = _options.ResourcesPath.Trim('/');
        var relative = string.IsNullOrEmpty(path) ? fileName : path + "/" + fileName;
        return string.Join("/", relative.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));
    }

    private static string ValidateHttpFileName(string fileName)
    {
        var normalized = JsonStoreCore.ValidateRelativeJsonPath(fileName);
        if (normalized.IndexOf('%') >= 0)
            throw new ArgumentException("Percent-encoded translation paths are not supported.", nameof(fileName));
        return normalized;
    }

    private static void ValidateResourcePath(string path)
    {
        if (path.IndexOfAny(['?', '#', '%', '\0', ':', '\\']) >= 0 ||
            path.Split('/').Any(static segment => segment is "." or "..") ||
            path.StartsWith("/", StringComparison.Ordinal))
            throw new ArgumentException("ResourcesPath must be a relative URL path.", nameof(path));
    }

    /// <summary>Releases the store's synchronization resources. The supplied HttpClient remains caller-owned.</summary>
    public void Dispose()
    {
        foreach (var state in _cultures.Values)
            state.Gate.Dispose();
        _manifestGate.Dispose();
    }
}
