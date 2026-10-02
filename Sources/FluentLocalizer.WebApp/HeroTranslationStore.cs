using System.Globalization;
using FluentLocalizer;

namespace FluentLocalizer.WebApp;

internal sealed class HeroTranslationStore : ITranslationStore
{
    private static readonly IReadOnlyDictionary<string, string> Templates = new Dictionary<string, string>
    {
        ["en-US"] = "Hello, {name}!",
        ["it-IT"] = "Ciao, {name}!",
        ["fr-FR"] = "Bonjour, {name} !",
        ["de-DE"] = "Hallo, {name}!",
        ["es-ES"] = "¡Hola, {name}!"
    };

    public string? GetTemplate(string key, CultureInfo culture) =>
        key == "welcome" && Templates.TryGetValue(culture.Name, out var template) ? template : null;

    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) =>
        Task.FromResult(GetTemplate(key, culture));
}
