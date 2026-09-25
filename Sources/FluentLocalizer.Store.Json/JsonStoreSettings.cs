namespace FluentLocalizer.Store.Json;

/// <summary>Shared settings for JSON translation stores.</summary>
public class JsonStoreSettings
{
    /// <summary>Gets or sets the fallback culture. Defaults to <c>en-US</c>.</summary>
    public string FallbackCulture { get; set; } = "en-US";

    /// <summary>Gets or sets whether missing translation files throw exceptions.</summary>
    /// <remarks>When disabled, filesystem and embedded-resource stores skip individual file read or JSON parse errors. HTTP request failures and invalid HTTP JSON always throw.</remarks>
    public bool ThrowOnMissingStore { get; set; }

    /// <summary>Gets mappings from culture names to JSON file names.</summary>
    public IDictionary<string, string> FileMappings { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
