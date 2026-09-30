namespace FluentLocalizer.Store.Json;

/// <summary>Options for <see cref="HttpJsonStore"/>.</summary>
public sealed class HttpJsonStoreOptions : JsonStoreSettings
{
    /// <summary>Gets or sets the relative path to the culture JSON files. Defaults to <c>locales</c>.</summary>
    public string ResourcesPath { get; set; } = "locales";
}
