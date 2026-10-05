using System.Globalization;

namespace FluentLocalizer.WebApp;

// The site's catalog uses the same translator as library consumers.
internal sealed partial class SiteText(ITranslator translator)
{
    public CultureInfo Culture { get; set; } = CultureInfo.GetCultureInfo("en");

    public object BrowserLabels => new
    {
        loading = this["Ui:Loading"],
        errorTitle = this["Ui:ErrorTitle"],
        error = this["Ui:UnhandledError"],
        reload = this["Ui:Reload"],
        dismiss = this["Ui:Dismiss"]
    };

    public string this[string key] => Request(key).Resolve();

    // Catalogs stay in sentence case; display casing such as eyebrows comes from the library's own transform.
    public string this[string key, LetterCase letterCase] => Request(key).WithCase(letterCase).Resolve();

    public string Format(string key, string name, object? value) => Request(key).WithArg(name, value).Resolve();

    private TranslationBuilder Request(string key) => translator.Get(key).WithCulture(Culture);

    public static CultureInfo SelectCulture(IEnumerable<string>? languages)
    {
        foreach (var language in languages ?? [])
        {
            if (string.IsNullOrWhiteSpace(language)) continue;
            try
            {
                var culture = CultureInfo.GetCultureInfo(language);
                if (culture.TwoLetterISOLanguageName is "en" or "it" or "fr" or "de" or "es")
                    return culture;
            }
            catch (CultureNotFoundException) { }
        }
        return CultureInfo.GetCultureInfo("en");
    }
}
