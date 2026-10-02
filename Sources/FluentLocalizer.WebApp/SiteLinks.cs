using System.Reflection;

namespace FluentLocalizer.WebApp;

/// <summary>External links and facts about the project shown across the site.</summary>
internal static class SiteLinks
{
    public const string Repository = "https://github.com/berik-99/FluentLocalizer";
    public const string License = Repository + "/blob/main/LICENSE";
    public const string Examples = Repository + "/tree/main/Examples";

    /// <summary>The version of the referenced core package; all packages are released together.</summary>
    public static readonly string PackageVersion = ReadPackageVersion();

    public static string NuGet(string packageId) => $"https://www.nuget.org/packages/{packageId}";

    public static string Readme(string project) => $"{Repository}/blob/main/Sources/{project}/Assets/README.md";

    private static string ReadPackageVersion()
    {
        var version = typeof(Translator).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? typeof(Translator).Assembly.GetName().Version?.ToString(3)
            ?? "1.0.0";
        // Drop SourceLink's "+commit" suffix.
        return version.Split('+')[0];
    }
}
