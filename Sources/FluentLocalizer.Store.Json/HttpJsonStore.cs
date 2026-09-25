using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FluentLocalizer.Store.Json;

/// <summary>
/// Loads culture JSON files over HTTP and caches them for subsequent translation lookups.
/// </summary>
/// <remarks>
/// The supplied <see cref="HttpClient"/> remains owned by the caller. Culture files are cached in memory and nested
/// object keys are addressed with colon-separated segments. Synchronous lookups require the requested culture to have
/// been loaded first; asynchronous lookups load it on demand.
/// </remarks>
/// <param name="httpClient">The client used to request translation files.</param>
/// <param name="options">Options for file paths and culture fallback.</param>
/// <exception cref="ArgumentNullException"><paramref name="httpClient"/> is <see langword="null"/>.</exception>
public sealed class HttpJsonStore(HttpClient httpClient, HttpJsonStoreOptions? options = null) : ITranslationStore, IDisposable
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly HttpJsonStoreOptions _options = options ?? new HttpJsonStoreOptions();
    private readonly ConcurrentDictionary<string, JsonDocument> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentBag<JsonDocument> _retiredDocuments = [];
    private readonly ConcurrentDictionary<string, byte> _loadedCultures = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    /// <summary>
    /// Loads the candidate JSON files for a culture into memory. Call this during application startup when synchronous
    /// <see cref="ITranslationStore.GetTemplate"/> lookups are needed.
    /// </summary>
    /// <param name="culture">The culture whose specific, neutral, and fallback files should be loaded.</param>
    /// <param name="cancellationToken">A token that can cancel the HTTP requests.</param>
    /// <returns>A task that completes when candidate files have been loaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <exception cref="HttpRequestException">A request failed or the server returned an unsuccessful status code other than not found.</exception>
    /// <exception cref="JsonException">A retrieved file does not contain valid JSON.</exception>
    /// <exception cref="FileNotFoundException"><see cref="JsonStoreSettings.ThrowOnMissingStore"/> is enabled and no candidate file exists.</exception>
    public Task LoadAsync(CultureInfo culture, CancellationToken cancellationToken = default)
        => culture is null ? throw new ArgumentNullException(nameof(culture)) : LoadCultureAsync(culture, cancellationToken);

    /// <summary>
    /// Loads candidate JSON files for each culture into memory.
    /// </summary>
    /// <param name="cultures">Culture names to preload. The configured fallback culture is loaded as well.</param>
    /// <param name="cancellationToken">A token that can cancel the HTTP requests.</param>
    /// <returns>A task that completes when the requested and fallback cultures have been loaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cultures"/> is <see langword="null"/>.</exception>
    /// <exception cref="CultureNotFoundException">A supplied culture name or the configured fallback culture is invalid.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <exception cref="HttpRequestException">A request failed or the server returned an unsuccessful status code other than not found.</exception>
    /// <exception cref="JsonException">A retrieved file does not contain valid JSON.</exception>
    /// <exception cref="FileNotFoundException"><see cref="JsonStoreSettings.ThrowOnMissingStore"/> is enabled and no candidate file exists for a loaded culture.</exception>
    public async Task LoadAsync(IEnumerable<string> cultures, CancellationToken cancellationToken = default)
    {
        if (cultures is null) throw new ArgumentNullException(nameof(cultures));
        foreach (var cultureName in cultures.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await LoadCultureAsync(CultureInfo.GetCultureInfo(cultureName), cancellationToken).ConfigureAwait(false);
        }

        await LoadCultureAsync(CultureInfo.GetCultureInfo(_options.FallbackCulture), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Re-downloads the files for cultures previously loaded by this store.
    /// Call this after translation files change on the server. A newly created store starts empty;
    /// call <see cref="LoadAsync(IEnumerable{string}, CancellationToken)"/> during app startup to load its cultures.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the HTTP requests.</param>
    /// <returns>A task that completes when previously loaded cultures have been refreshed.</returns>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    /// <exception cref="HttpRequestException">A request failed or the server returned an unsuccessful status code other than not found.</exception>
    /// <exception cref="JsonException">A retrieved file does not contain valid JSON.</exception>
    /// <exception cref="FileNotFoundException"><see cref="JsonStoreSettings.ThrowOnMissingStore"/> is enabled and no candidate file is available during the refresh.</exception>
    public async Task RefreshStoreAsync(CancellationToken cancellationToken = default)
    {
        var cultures = _loadedCultures.Keys.ToArray();
        foreach (var cultureName in cultures)
        {
            await LoadCultureAsync(CultureInfo.GetCultureInfo(cultureName), cancellationToken, forceRefresh: true)
                .ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException"><paramref name="culture"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled while loading a culture file.</exception>
    /// <exception cref="HttpRequestException">A request failed or the server returned an unsuccessful status code other than not found.</exception>
    /// <exception cref="JsonException">A retrieved file does not contain valid JSON.</exception>
    /// <exception cref="FileNotFoundException"><see cref="JsonStoreSettings.ThrowOnMissingStore"/> is enabled and no candidate file exists.</exception>
    public async Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
    {
        await LoadCultureAsync(culture, cancellationToken).ConfigureAwait(false);

        var template = FindTemplate(key, culture);
        if (template is not null || culture.Name.Equals(_options.FallbackCulture, StringComparison.OrdinalIgnoreCase))
            return template;

        var fallbackCulture = CultureInfo.GetCultureInfo(_options.FallbackCulture);
        await LoadCultureAsync(fallbackCulture, cancellationToken).ConfigureAwait(false);
        return FindTemplate(key, fallbackCulture);
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The requested culture or required fallback culture has not been loaded.</exception>
    public string? GetTemplate(string key, CultureInfo culture)
    {
        EnsureLoaded(culture);

        var template = FindTemplate(key, culture);
        if (template is not null || culture.Name.Equals(_options.FallbackCulture, StringComparison.OrdinalIgnoreCase))
            return template;

        var fallbackCulture = CultureInfo.GetCultureInfo(_options.FallbackCulture);
        EnsureLoaded(fallbackCulture);
        return FindTemplate(key, fallbackCulture);
    }

    private async Task LoadCultureAsync(
        CultureInfo culture,
        CancellationToken cancellationToken,
        bool forceRefresh = false)
    {
        if (!forceRefresh && _loadedCultures.ContainsKey(culture.Name))
            return;

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!forceRefresh && _loadedCultures.ContainsKey(culture.Name))
                return;

            var candidates = ResolveCandidates(culture);
            var foundFile = false;

            foreach (var fileName in candidates)
            {
                if (!forceRefresh && _cache.ContainsKey(fileName))
                {
                    foundFile = true;
                    continue;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, GetRequestUri(fileName));
                request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    if (forceRefresh && _cache.TryRemove(fileName, out var removedDocument))
                        _retiredDocuments.Add(removedDocument);
                    continue;
                }

                response.EnsureSuccessStatusCode();
#if NET8_0_OR_GREATER
                await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
                using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
                var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (forceRefresh)
                {
                    JsonDocument? oldDocument = null;
                    _cache.AddOrUpdate(fileName, document, (_, previous) =>
                    {
                        oldDocument = previous;
                        return document;
                    });
                    if (oldDocument is not null)
                        _retiredDocuments.Add(oldDocument);
                    foundFile = true;
                }
                else if (_cache.TryAdd(fileName, document))
                {
                    foundFile = true;
                }
                else
                {
                    document.Dispose();
                }
            }

            if (!foundFile && _options.ThrowOnMissingStore)
                throw new FileNotFoundException($"No translation files were found for culture '{culture.Name}'.");

            _loadedCultures.TryAdd(culture.Name, 0);
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private void EnsureLoaded(CultureInfo culture)
    {
        if (!_loadedCultures.ContainsKey(culture.Name))
        {
            throw new InvalidOperationException(
                $"Translation files for culture '{culture.Name}' have not been loaded. " +
                "Call LoadAsync before using synchronous translation resolution, or use ResolveAsync.");
        }
    }

    private string? FindTemplate(string key, CultureInfo culture)
    {
        foreach (var fileName in ResolveCandidates(culture))
        {
            if (_cache.TryGetValue(fileName, out var document) && JsonStoreCore.TryGetValue(document.RootElement, key, out var value))
                return value;
        }

        return null;
    }

    private List<string> ResolveCandidates(CultureInfo culture) =>
        JsonStoreCore.ResolveCandidates(culture, _options.FallbackCulture, _options.FileMappings);

    private string GetRequestUri(string fileName)
    {
        var path = _options.ResourcesPath.Trim('/');
        var relativePath = string.IsNullOrEmpty(path) ? fileName : $"{path}/{fileName}";
        return string.Join("/", relativePath.Split('/').Select(Uri.EscapeDataString));
    }

    /// <summary>
    /// Releases cached JSON documents. The supplied <see cref="HttpClient"/> remains owned by its caller.
    /// </summary>
    public void Dispose()
    {
        foreach (var document in _cache.Values)
            document.Dispose();

        foreach (var document in _retiredDocuments)
            document.Dispose();

        _loadLock.Dispose();
    }
}
