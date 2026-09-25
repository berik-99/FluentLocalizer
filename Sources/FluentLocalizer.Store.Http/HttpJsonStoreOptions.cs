namespace FluentLocalizer.Store.Http;

/// <summary>
/// Configures an HTTP-backed JSON translation store.
/// </summary>
public sealed class HttpJsonStoreOptions
{
    /// <summary>
    /// Gets or sets the relative path containing culture JSON files.
    /// The path is relative to <see cref="HttpClient.BaseAddress"/> and defaults to <c>locales</c>.
    /// </summary>
    public string ResourcesPath { get; set; } = "locales";

    /// <summary>
    /// Gets or sets the fallback culture. Defaults to <c>en-US</c>.
    /// </summary>
    /// <remarks>The store tries this culture after the requested culture and its neutral culture.</remarks>
    public string FallbackCulture { get; set; } = "en-US";

    /// <summary>
    /// Gets mappings between culture names and JSON file names.
    /// </summary>
    /// <remarks>Use a culture name such as <c>it-IT</c> as the key and a file name such as <c>italiano.json</c> as the value. Mappings are checked before conventional culture and neutral-culture filenames.</remarks>
    public IDictionary<string, string> FileMappings { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets whether loading a culture with no available JSON file throws an exception.
    /// </summary>
    /// <remarks>When enabled, loading throws <see cref="FileNotFoundException"/> if no candidate file can be loaded. A missing key in a successfully loaded file still returns <see langword="null"/>.</remarks>
    public bool ThrowOnMissingStore { get; set; }
}
