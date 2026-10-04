using System.Globalization;

namespace FluentLocalizer.WebApp;

// The site's catalog uses the same translator as library consumers.
internal sealed class SiteText(ITranslator translator)
{
    public CultureInfo Culture { get; set; } = CultureInfo.GetCultureInfo("en");

    public object BrowserLabels => new
    {
        loading = this["Ui:Loading"],
        error = this["Ui:UnhandledError"],
        reload = this["Ui:Reload"],
        dismiss = this["Ui:Dismiss"]
    };

    public string this[string key] => translator.Get(key).WithCulture(Culture).Resolve();

    public string Format(string key, string name, object? value) =>
        translator.Get(key).WithCulture(Culture).WithArg(name, value).Resolve();

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
