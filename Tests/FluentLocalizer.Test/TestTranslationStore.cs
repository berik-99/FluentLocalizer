using FluentLocalizer.Core;
using System.Globalization;

namespace FluentLocalizer.Test;

internal sealed class TestTranslationStore : ITranslationStore
{
    private readonly Dictionary<string, string?> _templates = new(StringComparer.OrdinalIgnoreCase);

    public void AddTemplate(string key, string? template) => _templates[key] = template;

    public string? GetTemplate(string key, CultureInfo culture)
    {
        if (_templates.TryGetValue(key, out var template))
        {
            return template;
        }

        throw new KeyNotFoundException($"No template found for key '{key}'.");
    }

    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) => Task.FromResult(GetTemplate(key, culture));
}
