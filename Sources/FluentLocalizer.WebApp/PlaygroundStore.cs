using System.Globalization;

namespace FluentLocalizer.WebApp;

/// <summary>The browser playground's single, editable translation template.</summary>
internal sealed class PlaygroundStore : ITranslationStore
{
    public string Template { get; set; } = string.Empty;

    public string? GetTemplate(string key, CultureInfo culture) =>
        key == "playground" ? Template : null;

    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetTemplate(key, culture));
}
