using FluentLocalizer.Polyfill;
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
    private readonly ReaderWriterLockSlim _catalogLock = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private ManifestCatalog? _manifest;
    private bool _manifestLoaded;
    private string? _manifestEtag;
    private DateTimeOffset? _manifestLastModified;
    private long _catalogVersion;

    private sealed record CachedFile(JsonElement Root, string? ETag, DateTimeOffset? LastModified);

    private sealed class ManifestCatalog(string[] files)
    {
        internal readonly string[] Files = files;
        internal readonly HashSet<string> FileSet = new(files, StringComparer.OrdinalIgnoreCase);
        internal readonly string[] Cultures = [.. files.Select(JsonStoreCore.CultureFromPath)
            .Where(static name => name is not null).Select(static name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)];
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
        if (_options.MaxDocumentBytes < 0) throw new ArgumentOutOfRangeException(nameof(options), "MaxDocumentBytes cannot be negative.");
        ValidateResourcePath(_options.ResourcesPath);
        foreach (var mapping in _options.FileMappings.Values)
            ValidateHttpFileName(mapping);
    }

    /// <summary>
    /// Preloads a culture and its fallback, including namespace files listed in manifest.json when present.
    /// </summary>
    public async Task LoadAsync(CultureInfo culture, CancellationToken cancellationToken = default)
    {
        Guard.IfNull(culture);
        await LoadCultureAsync(culture, null, cancellationToken).ConfigureAwait(false);
        if (!culture.Name.Equals(_options.FallbackCulture, StringComparison.OrdinalIgnoreCase))
            await LoadCultureAsync(CultureInfo.GetCultureInfo(_options.FallbackCulture), null, cancellationToken).ConfigureAwait(false);
        EnsureAvailable(culture);
    }

    /// <summary>
    /// Preloads the requested cultures and fallback, including namespace files listed in manifest.json when present.
    /// </summary>
    public async Task LoadAsync(IEnumerable<string> cultures, CancellationToken cancellationToken = default)
    {
        Guard.IfNull(cultures);
        var requested = cultures.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var name in requested)
            await LoadCultureAsync(CultureInfo.GetCultureInfo(name), null, cancellationToken).ConfigureAwait(false);
        await LoadCultureAsync(CultureInfo.GetCultureInfo(_options.FallbackCulture), null, cancellationToken).ConfigureAwait(false);
        EnsureAvailable(CultureInfo.GetCultureInfo(_options.FallbackCulture));
        foreach (var name in requested)
            EnsureAvailable(CultureInfo.GetCultureInfo(name));
    }

    /// <summary>
    /// Refreshes loaded cultures atomically. An invalid response leaves the previous culture snapshot available.
    /// </summary>
    public async Task RefreshStoreAsync(CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _manifestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var staged = new HttpJsonStore(_httpClient, _options);
                _catalogLock.EnterReadLock();
                try
                {
                    staged._manifest = _manifest;
                    staged._manifestLoaded = _manifestLoaded;
                    staged._manifestEtag = _manifestEtag;
                    staged._manifestLastModified = _manifestLastModified;
                    foreach (var pair in _cultures)
                    {
                        staged._cultures[pair.Key] = new CultureCache
                        {
                            Documents = Volatile.Read(ref pair.Value.Documents),
                            Attempted = new HashSet<string>(pair.Value.Attempted, StringComparer.OrdinalIgnoreCase)
                        };
                    }
                }
                finally { _catalogLock.ExitReadLock(); }

                await staged.RefreshInPlaceAsync(cancellationToken).ConfigureAwait(false);
                var states = staged._cultures.Keys.OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                    .Select(name => _cultures.GetOrAdd(name, static _ => new CultureCache())).ToArray();
                var acquired = 0;
                try
                {
                    foreach (var state in states)
                    {
                        await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                        acquired++;
                    }
                    _catalogLock.EnterWriteLock();
                    try
                    {
                        _manifest = staged._manifest;
                        _manifestLoaded = staged._manifestLoaded;
                        _manifestEtag = staged._manifestEtag;
                        _manifestLastModified = staged._manifestLastModified;
                        foreach (var pair in staged._cultures)
                        {
                            var target = _cultures[pair.Key];
                            var attempted = pair.Value.Attempted;
                            var documents = Volatile.Read(ref pair.Value.Documents);
                            if (_manifest is null)
                            {
                                attempted = new HashSet<string>(attempted.Concat(target.Attempted), StringComparer.OrdinalIgnoreCase);
                                if (documents is not null && target.Documents is not null)
                                {
                                    documents = new Dictionary<string, CachedFile>(documents, StringComparer.OrdinalIgnoreCase);
                                    foreach (var file in target.Documents)
                                        if (!pair.Value.Attempted.Contains(file.Key)) documents[file.Key] = file.Value;
                                }
                            }
                            target.Attempted = attempted;
                            Volatile.Write(ref target.Documents, documents);
                        }
                        _catalogVersion++;
                    }
                    finally { _catalogLock.ExitWriteLock(); }
                }
                finally
                {
                    for (var index = acquired - 1; index >= 0; index--) states[index].Gate.Release();
                }
            }
            finally { _manifestGate.Release(); }
        }
        finally { _refreshGate.Release(); }
    }

    private async Task RefreshInPlaceAsync(CancellationToken cancellationToken)
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
        Guard.IfNull(culture);
        await LoadCultureAsync(culture, key, cancellationToken).ConfigureAwait(false);
        _catalogLock.EnterReadLock();
        try
        {
            var value = FindTemplate(key, culture);
            if (value is not null || culture.Name.Equals(_options.FallbackCulture, StringComparison.OrdinalIgnoreCase))
            {
                EnsureAvailable(culture);
                return value;
            }
        }
        finally { _catalogLock.ExitReadLock(); }
        var fallback = CultureInfo.GetCultureInfo(_options.FallbackCulture);
        await LoadCultureAsync(fallback, key, cancellationToken).ConfigureAwait(false);
        _catalogLock.EnterReadLock();
        try
        {
            EnsureAvailable(culture);
            return FindTemplate(key, culture) ?? FindTemplate(key, fallback);
        }
        finally { _catalogLock.ExitReadLock(); }
    }

    /// <inheritdoc />
    public string? GetTemplate(string key, CultureInfo culture)
    {
        _catalogLock.EnterReadLock();
        try
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
        finally { _catalogLock.ExitReadLock(); }
    }

    private async Task LoadCultureAsync(CultureInfo culture, string? key, CancellationToken cancellationToken, bool forceRefresh = false)
    {
        await LoadManifestAsync(cancellationToken).ConfigureAwait(false);
        var state = _cultures.GetOrAdd(culture.Name, static _ => new CultureCache());
        while (true)
        {
            await state.Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var version = Volatile.Read(ref _catalogVersion);
                var candidates = Candidates(culture, key);
                var current = Volatile.Read(ref state.Documents);
                if (!forceRefresh && current is not null && candidates.All(state.Attempted.Contains)) return;
                var manifest = Volatile.Read(ref _manifest);
                var search = forceRefresh
                    ? state.Attempted.Union(candidates, StringComparer.OrdinalIgnoreCase)
                        .Where(file => manifest?.FileSet.Contains(file) != false)
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
                    if (_options.MaxDocumentBytes > 0)
                        await response.Content.LoadIntoBufferAsync(_options.MaxDocumentBytes).ConfigureAwait(false);
#if NET8_0_OR_GREATER
                    await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                    using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#endif
                    using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                    next[fileName] = new CachedFile(document.RootElement.Clone(), response.Headers.ETag?.ToString(), response.Content.Headers.LastModified);
                    if (!forceRefresh && key is not null && HasValue(next[fileName].Root, fileName, key))
                        break;
                }

                _catalogLock.EnterWriteLock();
                try
                {
                    if (_catalogVersion != version) continue;
                    state.Attempted = forceRefresh ? new HashSet<string>(attempted, StringComparer.OrdinalIgnoreCase) :
                        new HashSet<string>(state.Attempted.Concat(attempted), StringComparer.OrdinalIgnoreCase);
                    Volatile.Write(ref state.Documents, next);
                    return;
                }
                finally { _catalogLock.ExitWriteLock(); }
            }
            finally
            {
                state.Gate.Release();
            }
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
            ManifestCatalog? nextManifest = null;
            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
                if (_options.MaxDocumentBytes > 0)
                    await response.Content.LoadIntoBufferAsync(_options.MaxDocumentBytes).ConfigureAwait(false);
#if NET8_0_OR_GREATER
                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#endif
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    throw new JsonException("manifest.json must contain an array of relative JSON paths.");
                var files = document.RootElement.EnumerateArray().Select(entry =>
                    entry.ValueKind == JsonValueKind.String ? ValidateHttpFileName(entry.GetString()!) :
                    throw new JsonException("manifest.json entries must be JSON file paths.")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                nextManifest = new ManifestCatalog(files);
            }
            _catalogLock.EnterWriteLock();
            try
            {
                Volatile.Write(ref _manifest, nextManifest);
                _manifestEtag = response.Headers.ETag?.ToString();
                _manifestLastModified = response.Content?.Headers.LastModified;
                Volatile.Write(ref _manifestLoaded, true);
            }
            finally { _catalogLock.ExitWriteLock(); }
        }
        finally { _manifestGate.Release(); }
    }

    private List<string> Candidates(CultureInfo culture, string? key)
    {
        var manifest = Volatile.Read(ref _manifest);
        if (manifest is null)
        {
            return key is null ? JsonStoreCore.ResolveCandidates(culture, culture.Name, _options.FileMappings)
                : JsonStoreCore.ResolveFileCandidates(culture, culture.Name, _options.FileMappings, key);
        }

        var available = manifest.Cultures
            .Concat(_options.FileMappings.Where(mapping => manifest.FileSet.Contains(mapping.Value.Replace('\\', '/')))
                .Select(static mapping => mapping.Key))
            .ToArray();
        var selected = JsonStoreCore.SelectCulture(culture, available);
        if (selected is null) return [];
        if (key is null)
        {
            return [.. manifest.Files.Where(file => JsonStoreCore.CultureFromPath(file)?.Equals(selected, StringComparison.OrdinalIgnoreCase) == true ||
                (_options.FileMappings.TryGetValue(selected, out var mapped) && file.Equals(mapped.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase)))
                .OrderBy(static file => file, StringComparer.OrdinalIgnoreCase)];
        }

        return [.. JsonStoreCore.ResolveFileCandidates(CultureInfo.GetCultureInfo(selected), selected, _options.FileMappings, key)
            .Select(static candidate => candidate.Replace('\\', '/'))
            .Where(manifest.FileSet.Contains)];
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
            if (JsonStoreCore.TryGetValue(root.Root, key, out var value)
                || (JsonStoreCore.IsNamespaceFile(fileName, key)
                && JsonStoreCore.TryGetValue(root.Root, JsonStoreCore.GetLookupKey(key), out value)))
            {
                return value;
            }
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
        if (Volatile.Read(ref _manifest) is null && !culture.IsNeutralCulture
            && !culture.Name.Equals(fallback.Name, StringComparison.OrdinalIgnoreCase)
            && culture.Parent.Name.Equals(fallback.Parent.Name, StringComparison.OrdinalIgnoreCase)
            && !HasDocuments(culture))
        {
            throw new FileNotFoundException($"No translation files were found for culture '{culture.Name}'; only another regional variant is available.");
        }

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
        if (normalized.Contains('%'))
            throw new ArgumentException("Percent-encoded translation paths are not supported.", nameof(fileName));
        return normalized;
    }

    private static void ValidateResourcePath(string path)
    {
        if (path.IndexOfAny(['?', '#', '%', '\0', ':', '\\']) >= 0
            || path.Split('/').Any(static segment => segment is "." or "..")
            || path.StartsWith('/'))
        {
            throw new ArgumentException("ResourcesPath must be a relative URL path.", nameof(path));
        }
    }

    /// <summary>Releases the store's synchronization resources. The supplied HttpClient remains caller-owned.</summary>
    public void Dispose()
    {
        foreach (var state in _cultures.Values)
            state.Gate.Dispose();
        _manifestGate.Dispose();
        _refreshGate.Dispose();
        _catalogLock.Dispose();
    }
}
