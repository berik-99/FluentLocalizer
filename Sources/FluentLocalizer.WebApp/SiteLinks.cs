using System.Reflection;

namespace FluentLocalizer.WebApp;

/// <summary>External links and facts about the project shown across the site.</summary>
internal static class SiteLinks
{
    public const string Repository = "https://github.com/berik-99/FluentLocalizer";
    public const string License = Repository + "/blob/main/LICENSE";
    public const string Examples = Repository + "/tree/main/Examples";
    public const string ThirdPartyNotices = Repository + "/blob/main/THIRD-PARTY-NOTICES.md";

    /// <summary>The version read from the referenced core assembly.</summary>
    public static readonly string PackageVersion = ReadPackageVersion();

    public static string NuGet(string packageId) => $"https://www.nuget.org/packages/{packageId}";

    public static string Readme(string project) => $"{Repository}/blob/main/Sources/{project}/Assets/README.md";

    /// <summary>The value of a query parameter of <paramref name="uri"/>, or null when it is missing.</summary>
    public static string? QueryValue(string uri, string name)
    {
        foreach (var pair in new Uri(uri).Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]) == name)
                return parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : "";
        }
        return null;
    }

    private static string ReadPackageVersion()
    {
        var version = typeof(Translator).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(Translator).Assembly.GetName().Version?.ToString(3)
            ?? "1.0.0";
        // Drop SourceLink's "+commit" suffix.
        return version.Split('+')[0];
    }
}
