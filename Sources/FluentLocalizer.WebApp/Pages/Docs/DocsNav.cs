namespace FluentLocalizer.WebApp.Pages.Docs;

/// <summary>A documentation page: its catalog key under <c>Docs</c>, its route and its indentation in the sidebar.</summary>
/// <remarks>The page reads <c>Docs:{Key}:Nav</c>, <c>:Title</c> and <c>:Description</c> from the site catalogs.</remarks>
internal sealed record DocsPageInfo(string Key, string Route, int Level = 0);

/// <summary>A titled group of pages in the documentation sidebar, named by <c>Docs:Nav:{Key}</c>.</summary>
internal sealed record DocsGroup(string Key, DocsPageInfo[] Pages);

/// <summary>The documentation outline, in reading order.</summary>
/// <remarks>Register a new page here to list it in the sidebar and in the previous/next links. New stores join the Stores group, below the family they belong to.</remarks>
internal static class DocsNav
{
    public static readonly DocsGroup[] Groups =
    [
        new("Start",
        [
            new("Intro", "docs"),
            new("QuickStart", "docs/quick-start")
        ]),
        new("Guides",
        [
            new("Templates", "docs/templates"),
            new("Requests", "docs/requests"),
            new("Culture", "docs/culture"),
            new("Errors", "docs/errors"),
            new("Logging", "docs/logging"),
            new("Injection", "docs/dependency-injection")
        ]),
        new("Stores",
        [
            new("Stores", "docs/stores"),
            new("Json", "docs/stores/json"),
            new("JsonFile", "docs/stores/json/file", 1),
            new("JsonEmbedded", "docs/stores/json/embedded", 1),
            new("JsonHttp", "docs/stores/json/http", 1),
            new("CustomStore", "docs/stores/custom")
        ]),
        new("Reference",
        [
            new("Api", "docs/api"),
            new("Migration", "docs/migration")
        ])
    ];

    public static readonly DocsPageInfo[] Pages = [.. Groups.SelectMany(group => group.Pages)];

    public static DocsGroup GroupOf(DocsPageInfo page) => Groups.First(group => group.Pages.Contains(page));

    public static DocsPageInfo Find(string key) => Pages.First(page => page.Key == key);
}
