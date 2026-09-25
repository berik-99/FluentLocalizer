using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FluentLocalizer.Store.Http;

/// <summary>
/// Loads culture JSON files over HTTP and caches them for subsequent translation lookups.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="HttpJsonStore"/> class.
/// </remarks>
/// <param name="httpClient">The client used to request translation files.</param>
/// <param name="options">Options for file paths and culture fallback.</param>
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
    public Task LoadAsync(CultureInfo culture, CancellationToken cancellationToken = default)
        => culture is null ? throw new ArgumentNullException(nameof(culture)) : LoadCultureAsync(culture, cancellationToken);

    /// <summary>
    /// Loads candidate JSON files for each culture into memory.
    /// </summary>
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
            if (_cache.TryGetValue(fileName, out var document) && TryGetValue(document.RootElement, key, out var value))
                return value;
        }

        return null;
    }

    private List<string> ResolveCandidates(CultureInfo culture)
    {
        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string fileName)
        {
            if (!string.IsNullOrWhiteSpace(fileName) && seen.Add(fileName))
                candidates.Add(fileName);
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

            var fallback = CultureInfo.GetCultureInfo(_options.FallbackCulture);
            Add($"{fallback.Name}.json");
            Add($"{fallback.TwoLetterISOLanguageName}.json");
        }

        return candidates;
    }

    private string GetRequestUri(string fileName)
    {
        var path = _options.ResourcesPath.Trim('/');
        var relativePath = string.IsNullOrEmpty(path) ? fileName : $"{path}/{fileName}";
        return string.Join("/", relativePath.Split('/').Select(Uri.EscapeDataString));
    }

    private static bool TryGetValue(JsonElement root, string key, out string value)
    {
        value = string.Empty;
        if (string.IsNullOrWhiteSpace(key))
            return false;

        var current = root;
        foreach (var segment in key.Split([':'], StringSplitOptions.RemoveEmptyEntries).Select(static item => item.Trim()))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                return false;
        }

        if (current.ValueKind != JsonValueKind.String)
            return false;

        value = current.GetString() ?? string.Empty;
        return true;
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
