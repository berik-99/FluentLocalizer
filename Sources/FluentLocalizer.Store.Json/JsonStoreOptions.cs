using System.Reflection;

namespace FluentLocalizer.Store.Json;

/// <summary>Legacy options for <see cref="JsonStore"/>. Use the store-specific options with the three dedicated JSON stores.</summary>
[Obsolete("JsonStoreOptions is deprecated. Use JsonFileStoreOptions, EmbeddedJsonStoreOptions, or HttpJsonStoreOptions.")]
public sealed class JsonStoreOptions : JsonStoreSettings
{
    /// <summary>Gets or sets the path containing translation files.</summary>
    public string ResourcesPath { get; set; } = "Locales";

    /// <summary>Gets or sets the legacy location mode.</summary>
    public JsonStoreLocation SearchMode { get; set; } = JsonStoreLocation.FileSystem;

    /// <summary>Gets or sets the assembly used for embedded resources.</summary>
    public Assembly? ResourceAssembly { get; set; }

    /// <summary>Enables automatic reload of filesystem files.</summary>
    public bool ReloadOnChange { get; set; }
}

/// <summary>Legacy location values for <see cref="JsonStore"/>.</summary>
public enum JsonStoreLocation
{
    /// <summary>Local filesystem.</summary>
    FileSystem,
    /// <summary>Embedded assembly resources.</summary>
    EmbeddedResources
}
