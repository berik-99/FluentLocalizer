using System.Reflection;

namespace FluentLocalizer.Store.Json;

/// <summary>Options for <see cref="EmbeddedJsonStore"/>.</summary>
public sealed class EmbeddedJsonStoreOptions : JsonStoreSettings
{
    /// <summary>Gets or sets the assembly containing translation resources. Defaults to the entry assembly.</summary>
    public Assembly? ResourceAssembly { get; set; }

    /// <summary>Gets or sets the resource folder name used to select embedded JSON files.</summary>
    public string ResourcesPath { get; set; } = "Locales";
}
