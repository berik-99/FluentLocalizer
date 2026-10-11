using FluentLocalizer.WebApp.Pages.Docs.Core.V1;
using StoreJsonV1 = FluentLocalizer.WebApp.Pages.Docs.StoreJson.V1;

namespace FluentLocalizer.WebApp.Pages.Docs;

/// <summary>A documentation page: its catalog key, its path inside the set, its component and its indentation in the sidebar.</summary>
/// <remarks>
/// A guide reads <c>Docs:{Set}:{Key}:Nav</c>, <c>:Title</c> and <c>:Description</c> from the site catalogs.
/// A <paramref name="Shared"/> page, such as the API reference, is the same in every set and reads <c>Docs:{Key}</c>.
/// </remarks>
internal sealed record DocsPageInfo(string Key, string Path, Type Component, int Level = 0, bool Shared = false)
{
    public bool IsApi => Component == typeof(Api);
}

/// <summary>A titled group of pages in the documentation sidebar, named by <c>Docs:Nav:{Key}</c>.</summary>
internal sealed record DocsGroup(string Key, DocsPageInfo[] Pages);

/// <summary>The guides and API reference of one scope for one major version, served under <c>docs/{scope}/v{major}</c>.</summary>
/// <remarks>
/// <see cref="Key"/> names the catalog object of its guides, such as <c>Docs:CoreV1</c>, and the folder of its pages,
/// such as <c>Pages/Docs/Core/V1</c>. A set stays unchanged once a newer major version is released.
/// </remarks>
internal sealed record DocsSet(string ScopeId, int Major, string Key, DocsGroup[] Groups)
{
    public DocsPageInfo[] Pages { get; } = [.. Groups.SelectMany(group => group.Pages)];

    public ApiScope Scope => ApiReference.Scope(ScopeId);
    public string Version => $"{Major}.x";
    public string Route => $"docs/{ScopeId}/v{Major}";
    /// <summary>Major version 0 was the experimental initial development: its sets explain why they have no guides.</summary>
    public bool IsExperimental => Major == 0;
    public bool IsLatest => DocsSets.Latest(ScopeId) == this;

    /// <summary>The API versions documented by this set, oldest first.</summary>
    public ApiRelease[] Releases => Scope.VersionsOf(Major).ToArray();

    /// <summary>The newest published version of this set, or its newest preview before the first release.</summary>
    public string LatestRelease => Releases.LastOrDefault(release => release.Date is not null)?.Version ?? Releases[^1].Version;

    public string RouteOf(DocsPageInfo page) => page.Path.Length == 0 ? Route : $"{Route}/{page.Path}";

    public DocsPageInfo? Find(string key) => Pages.FirstOrDefault(page => page.Key == key);

    public DocsPageInfo? FindPath(string path) => Pages.FirstOrDefault(page => page.Path == path);

    public DocsPageInfo? ApiPage => Pages.FirstOrDefault(page => page.IsApi);

    public DocsGroup GroupOf(DocsPageInfo page) => Groups.First(group => group.Pages.Contains(page));

    /// <summary>The catalog key of a page's text, such as <c>Docs:CoreV1:Templates:Title</c> or <c>Docs:Api:Nav</c>.</summary>
    public string TextKey(DocsPageInfo page, string name) => page.Shared ? $"Docs:{page.Key}:{name}" : $"Docs:{Key}:{page.Key}:{name}";
}

/// <summary>Every documentation set, newest major version first within each scope.</summary>
/// <remarks>
/// To start the documentation of a new major version, copy the latest set's folder and catalog object
/// (for example <c>Core/V1</c> and <c>Docs:CoreV1</c> to <c>Core/V2</c> and <c>Docs:CoreV2</c>), register the copy here
/// and edit only the copy. The new set becomes the default once its scope publishes a version with that major number.
/// </remarks>
internal static class DocsSets
{
    private static readonly DocsPageInfo ApiPage = new("Api", "api", typeof(Api), Shared: true);

    // The 0.x releases were experimental, so their sets explain the missing guides and keep the API reference.
    private static readonly DocsGroup[] Experimental =
    [
        new("Versions", [new("Experimental", "", typeof(DocsExperimental), Shared: true)]),
        new("Reference", [ApiPage])
    ];

    public static readonly DocsSet[] All =
    [
        new("core", 1, "CoreV1",
        [
            new("Start",
            [
                new("Intro", "", typeof(DocsIntro)),
                new("QuickStart", "quick-start", typeof(DocsQuickStart))
            ]),
            new("Guides",
            [
                new("Templates", "templates", typeof(DocsTemplates)),
                new("Requests", "requests", typeof(DocsRequests)),
                new("Culture", "culture", typeof(DocsCulture)),
                new("Errors", "errors", typeof(DocsErrors)),
                new("Logging", "logging", typeof(DocsLogging)),
                new("Injection", "dependency-injection", typeof(DocsInjection))
            ]),
            new("Stores",
            [
                new("Stores", "stores", typeof(DocsStores)),
                new("CustomStore", "stores/custom", typeof(DocsCustomStore))
            ]),
            new("Reference",
            [
                ApiPage,
                new("Migration", "migration", typeof(DocsMigration))
            ])
        ]),
        new("core", 0, "CoreV0", Experimental),
        new("store-json", 1, "StoreJsonV1",
        [
            new("Stores",
            [
                new("Json", "", typeof(StoreJsonV1.DocsJson)),
                new("JsonFile", "file", typeof(StoreJsonV1.DocsJsonFile), 1),
                new("JsonEmbedded", "embedded", typeof(StoreJsonV1.DocsJsonEmbedded), 1),
                new("JsonHttp", "http", typeof(StoreJsonV1.DocsJsonHttp), 1)
            ]),
            new("Reference",
            [
                ApiPage,
                new("Migration", "migration", typeof(StoreJsonV1.DocsMigration))
            ])
        ]),
        new("store-json", 0, "StoreJsonV0", Experimental)
    ];

    public const string DefaultScope = "core";

    /// <summary>The scopes with documentation, in menu order.</summary>
    public static readonly string[] ScopeIds = [.. All.Select(set => set.ScopeId).Distinct()];

    /// <summary>The sets of a scope, newest major version first.</summary>
    public static IEnumerable<DocsSet> Of(string scopeId) => All.Where(set => set.ScopeId == scopeId).OrderByDescending(set => set.Major);

    /// <summary>The newest set whose major version has been published; a set for an unreleased major version is a preview.</summary>
    public static DocsSet Latest(string scopeId) =>
        Of(scopeId).FirstOrDefault(set => set.Releases.Any(release => release.Date is not null)) ?? Of(scopeId).First();

    /// <summary>Finds a set by its route segments, such as <c>core</c> and <c>v1</c>.</summary>
    public static DocsSet? Find(string scopeId, string version) =>
        All.FirstOrDefault(set => set.ScopeId == scopeId && $"v{set.Major}" == version);

    /// <summary>The set that documents a released version, such as <c>store-json</c> 0.1.0-alpha.</summary>
    public static DocsSet ForVersion(string scopeId, string version) =>
        Find(scopeId, $"v{ApiVersion.Major(version)}") ?? Latest(scopeId);

    /// <summary>The address of a type in the API reference of the set that documents <paramref name="version"/>, or of the latest set.</summary>
    public static string ApiLink(ApiType type, string? version = null)
    {
        var set = version is null ? Latest(type.Scope.Id) : ForVersion(type.Scope.Id, version);
        var query = version is null || version == set.LatestRelease ? "" : $"?version={Uri.EscapeDataString(version)}";
        return $"{set.Route}/api{query}#{type.Id}";
    }

    /// <summary>The scope's display name, from <c>Ui:Scope{Name}</c>, such as <c>Ui:ScopeStoreJson</c>.</summary>
    public static string ScopeNameKey(string scopeId) =>
        "Ui:Scope" + string.Concat(scopeId.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));

    /// <summary>
    /// Maps an address of the documentation before it was versioned, such as <c>stores/json/file</c> or <c>api#api-jsonfilestore</c>,
    /// to the matching page of the latest set.
    /// </summary>
    public static string? LegacyRoute(string path, string fragment)
    {
        string scope = DefaultScope;
        if (path == "stores/json" || path.StartsWith("stores/json/", StringComparison.Ordinal))
        {
            scope = "store-json";
            path = path["stores/json".Length..].TrimStart('/');
        }
        else if (path == "api" && ApiReference.FindById(fragment) is { } type)
        {
            scope = type.Scope.Id;
        }

        var set = Latest(scope);
        var page = set.FindPath(path);
        return page is null ? null : set.RouteOf(page) + (fragment.Length > 0 ? "#" + fragment : "");
    }
}
