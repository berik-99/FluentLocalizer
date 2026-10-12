using System.Reflection;

namespace FluentLocalizer.Store.Json;

/// <summary>Options for <see cref="EmbeddedJsonStore"/>.</summary>
public sealed class EmbeddedJsonStoreOptions : JsonStoreSettings
{
    /// <summary>Gets or sets the assembly containing translation resources. Defaults to the entry assembly.</summary>
    public Assembly? ResourceAssembly { get; set; }

    /// <summary>Gets or sets the resource folder name used to select embedded JSON files.</summary>
    public string ResourcesPath { get; set; } = "Locales";

    /// <summary>Gets or sets whether each JSON resource is read and parsed on its first lookup instead of during construction. Defaults to <see langword="false"/>.</summary>
    /// <remarks>Only the files a lookup needs are parsed, so cultures that are never requested stay out of memory. Invalid JSON is then reported by the first lookup that reads the file.</remarks>
    public bool LoadOnDemand { get; set; }
}
