using FluentLocalizer.Core;
using System.Globalization;

namespace FluentLocalizer.Samples.ConsoleApp;

class MemoryStore : ITranslationStore
{
    private readonly Dictionary<string, string> _storeIT = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Welcome"] = "{gender, select, male {Benvenuto} female {Benvenuta} other {Benvenuto/a}}, {name}! Hai {quantity, plural, one {un solo messaggio} other {# messaggi}}.",
        ["Notifications:MessageCount"] = "Notifica per {name}: hai {quantity, plural, one {un solo messaggio} other {# messaggi}}.",
        ["BrokenTemplate"] = "Messaggio non valido {name"
    };

    private readonly Dictionary<string, string> _storeEN = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Welcome"] = "Welcome, {name}! You have {quantity, plural, one {only one message} other {# messages}}.",
        ["Notifications:MessageCount"] = "Notification for {name}: you have {quantity, plural, one {one message} other {# messages}}.",
        ["BrokenTemplate"] = "Broken template {name"
    };

    public string? GetTemplate(string key, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var templates = culture.TwoLetterISOLanguageName switch
        {
            "it" => _storeIT,
            "en" => _storeEN,
            _ => _storeEN
        };

        return templates.TryGetValue(key, out var template)
            ? template
            : throw new KeyNotFoundException($"No template found for key '{key}'.");
    }

    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
        => Task.FromResult(GetTemplate(key, culture));
}