using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace FluentLocalizer.WebApp;

// Kept apart from SiteText.cs, which the catalog tests also compile for .NET Framework.
internal sealed partial class SiteText
{
    // Lets translators keep whole sentences: `code`, *emphasis* and [label](href) become markup, the rest stays text.
    public RenderFragment Rich(string key) => builder =>
    {
        var text = this[key];
        var position = 0;
        foreach (Match match in RichSpans().Matches(text))
        {
            builder.AddContent(0, text[position..match.Index]);
            if (match.Groups["code"].Success)
            {
                builder.OpenElement(1, "code");
                builder.AddContent(2, match.Groups["code"].Value);
                builder.CloseElement();
            }
            else if (match.Groups["em"].Success)
            {
                builder.OpenElement(3, "em");
                builder.AddContent(4, match.Groups["em"].Value);
                builder.CloseElement();
            }
            else
            {
                var href = match.Groups["href"].Value;
                builder.OpenElement(5, "a");
                builder.AddAttribute(6, "href", href);
                // External links open in a new tab, like the other outbound links of the site.
                if (href.StartsWith("https://", StringComparison.Ordinal))
                {
                    builder.AddAttribute(7, "target", "_blank");
                    builder.AddAttribute(8, "rel", "noopener noreferrer");
                }
                builder.AddContent(9, match.Groups["label"].Value);
                builder.CloseElement();
            }
            position = match.Index + match.Length;
        }
        builder.AddContent(10, text[position..]);
    };

    [GeneratedRegex(@"`(?<code>[^`]+)`|\*(?<em>[^*]+)\*|\[(?<label>[^\]]+)\]\((?<href>[^)\s]+)\)")]
    private static partial Regex RichSpans();
}
