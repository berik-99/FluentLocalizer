using FluentLocalizer.Core;
using System.Globalization;

namespace FluentLocalizer.Samples.ConsoleApp;

class MemoryTranslationStore : ITranslationStore
{
    private readonly Dictionary<string, string> _storeIT = new()
{
    { "Welcome", "{gender, select, male {Benvenuto} female {Benvenuta} other {Benvenuto/a}}, {name}! Hai {quantity, plural, one {un solo messaggio} other {# messaggi}}." }
};

    private readonly Dictionary<string, string> _storeEN = new()
{
    { "Welcome", "Welcome, {name}! You have {quantity, plural, one {only one message} other {# messages}}." }
};

    public string? GetTemplate(string key, CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "it" => _storeIT[key],
        "en" => _storeEN[key],
        _ => _storeEN[key]
    };

    public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) => Task.FromResult(GetTemplate(key, culture));
}