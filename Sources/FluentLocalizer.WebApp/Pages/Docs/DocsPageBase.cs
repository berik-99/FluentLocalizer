using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace FluentLocalizer.WebApp.Pages.Docs;

/// <summary>Base class of the documentation pages: resolves the page's own catalog entries under <c>Docs:{PageKey}</c>.</summary>
public abstract class DocsPageBase : ComponentBase
{
    [Inject] private ITranslator DocsTranslator { get; set; } = default!;

    /// <summary>Rerenders the page when the site language changes.</summary>
    [CascadingParameter] protected CultureInfo SiteCulture { get; set; } = CultureInfo.CurrentUICulture;

    /// <summary>The page's key in <see cref="DocsNav"/> and in the <c>Docs</c> catalog object.</summary>
    protected abstract string PageKey { get; }

    /// <summary>The sections listed under the page in the sidebar, as anchor id and title.</summary>
    protected virtual (string Id, string Title)[] Sections => [];

    /// <summary>Resolves <c>Docs:{PageKey}:{key}</c>.</summary>
    protected string T(string key) => DocsTranslator.Get($"Docs:{PageKey}:{key}");

    /// <summary>Resolves a label shared by every page, <c>Docs:Common:{key}</c>.</summary>
    protected string Common(string key) => DocsTranslator.Get($"Docs:Common:{key}");
}
