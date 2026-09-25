using System.Reflection;

namespace FluentLocalizer.Store.Json;

/// <summary>
/// Defines where translation files are resolved from.
/// </summary>
public enum JsonStoreLocation
{
    /// <summary>
    /// Reads translation files from the local filesystem.
    /// </summary>
    FileSystem,

    /// <summary>
    /// Reads translation files from embedded resources in an assembly.
    /// </summary>
    EmbeddedResources
}

/// <summary>
/// Provides configuration options for <see cref="JsonStore"/>.
/// </summary>
public class JsonStoreOptions
{
    /// <summary>
    /// Gets or sets the path containing translation files.
    /// Can be relative or absolute.
    /// Default value: <c>Locales</c>.
    /// </summary>
    public string ResourcesPath { get; set; } = "Locales";

    /// <summary>
    /// Gets or sets where translation files are resolved from.
    /// Default value: <c>FileSystem</c>.
    /// </summary>
    public JsonStoreLocation SearchMode { get; set; } = JsonStoreLocation.FileSystem;

    /// <summary>
    /// Gets or sets the assembly that should be inspected when <see cref="SearchMode"/>
    /// is <see cref="JsonStoreLocation.EmbeddedResources"/>.
    /// When not specified, the entry assembly is used.
    /// </summary>
    public Assembly? ResourceAssembly { get; set; }

    /// <summary>
    /// Gets or sets the fallback culture used when a translation
    /// cannot be found for the requested culture.
    /// Default value: <c>en-US</c>.
    /// </summary>
    public string FallbackCulture { get; set; } = "en-US";

    /// <summary>
    /// Enables automatic reload when translation files change.
    /// Default value: <c>false</c>.
    /// </summary>
    /// <remarks>This option applies only to <see cref="JsonStoreLocation.FileSystem"/>. Changes to embedded resources cannot be watched.</remarks>
    public bool ReloadOnChange { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether load errors or missing translation files should throw exceptions.
    /// Default value: <c>false</c>.
    /// </summary>
    /// <remarks>When <see langword="false"/>, files that cannot be loaded are skipped and missing lookups return <see langword="null"/>. When <see langword="true"/>, file discovery or parsing errors are surfaced, and a lookup throws <see cref="FileNotFoundException"/> if no candidate file exists.</remarks>
    public bool ThrowOnMissingStore { get; set; }

    /// <summary>
    /// Gets custom mappings between culture names and file names.
    /// Example:
    /// <code>
    /// options.FileMappings["it-IT"] = "Italiano.json";
    /// </code>
    /// Mapped names are considered before the conventional culture and neutral-culture filenames.
    /// </summary>
    /// <remarks>For example, map <c>it-IT</c> to <c>Italiano.json</c> when the file does not use the default culture name.</remarks>
    public IDictionary<string, string> FileMappings { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
