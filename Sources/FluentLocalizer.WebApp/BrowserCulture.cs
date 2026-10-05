using System.Globalization;

namespace FluentLocalizer.WebApp;

internal static class BrowserCulture
{
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
