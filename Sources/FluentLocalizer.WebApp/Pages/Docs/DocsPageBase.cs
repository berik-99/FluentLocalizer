using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace FluentLocalizer.WebApp.Pages.Docs;

/// <summary>Base class of the documentation pages: resolves the page's own catalog entries under <c>Docs:{Set}:{PageKey}</c>.</summary>
public abstract class DocsPageBase : ComponentBase
{
    [Inject] private ITranslator DocsTranslator { get; set; } = default!;

    /// <summary>Rerenders the page when the site language changes.</summary>
    [CascadingParameter] protected CultureInfo SiteCulture { get; set; } = CultureInfo.CurrentUICulture;

    /// <summary>The documentation set that lists the page, supplied by <see cref="DocsRouter"/>.</summary>
    [CascadingParameter] internal DocsSet Set { get; set; } = default!;

    /// <summary>The page's key in its <see cref="DocsSet"/> and in the set's catalog object.</summary>
    protected abstract string PageKey { get; }

    /// <summary>The sections listed under the page in the sidebar, as anchor id and title.</summary>
    protected virtual (string Id, string Title)[] Sections => [];

    /// <summary>Resolves <c>Docs:{Set}:{PageKey}:{key}</c>, such as <c>Docs:CoreV1:Templates:Title</c>, or <c>Docs:{PageKey}:{key}</c> for a page shared by every set.</summary>
    protected string T(string key) => DocsTranslator.Get(Set.TextKey(Set.Find(PageKey)!, key));

    /// <summary>Resolves a label shared by every page, <c>Docs:Common:{key}</c>.</summary>
    protected string Common(string key) => DocsTranslator.Get($"Docs:Common:{key}");
}
