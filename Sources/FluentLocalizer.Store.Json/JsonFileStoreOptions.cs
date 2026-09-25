namespace FluentLocalizer.Store.Json;

/// <summary>Options for <see cref="JsonFileStore"/>.</summary>
public sealed class JsonFileStoreOptions : JsonStoreSettings
{
    /// <summary>Gets or sets the directory containing translation JSON files. Relative paths use the application base directory.</summary>
    public string ResourcesPath { get; set; } = "Locales";

    /// <summary>Enables automatic reload when translation files change.</summary>
    public bool ReloadOnChange { get; set; }
}
