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
    public string FallbackCulture { get; set; } = "en-US";

    /// <summary>
    /// Gets mappings between culture names and JSON file names.
    /// </summary>
    public IDictionary<string, string> FileMappings { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets whether loading a culture with no available JSON file throws an exception.
    /// </summary>
    public bool ThrowOnMissingStore { get; set; }
}
