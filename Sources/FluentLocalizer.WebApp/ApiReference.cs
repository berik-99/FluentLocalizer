using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;

namespace FluentLocalizer.WebApp;

internal sealed class ApiCatalog
{
    public ApiScope[] Scopes { get; set; } = [];
    public ApiType[] Types { get; set; } = [];
}

/// <summary>Packages released together under one version line, such as Core (core and DI) or one store.</summary>
/// <remarks>The release tag of a scope is <c>{Id}-v{Version}</c>, such as <c>core-v1.0.0</c> or <c>store-json-v1.0.0</c>.</remarks>
internal sealed class ApiScope
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string[] Packages { get; set; } = [];
    public ApiRelease[] Versions { get; set; } = [];

    public string FirstVersion => Versions[0].Version;

    /// <summary>The newest published version, shown by default; later entries preview upcoming releases.</summary>
    public string LatestRelease => Versions.LastOrDefault(release => release.Date is not null)?.Version ?? Versions[^1].Version;

    public ApiRelease Release(string version) => Versions.First(release => release.Version == version);

    /// <summary>The versions of one major release line, such as 1.0.0 and 1.1.0 for 1.x.</summary>
    public IEnumerable<ApiRelease> VersionsOf(int major) => Versions.Where(release => release.Major == major);

    public IEnumerable<ApiType> Types => ApiReference.Types.Where(type => Packages.Contains(type.Package));
}

internal sealed class ApiRelease
{
    public string Version { get; set; } = "";
    /// <summary>Release date, or null for a version that is documented but not yet published.</summary>
    public string? Date { get; set; }
    public string? Summary { get; set; }
    /// <summary>Verified Git revision for links to the implementation shipped in this release.</summary>
    public string SourceRevision { get; set; } = "main";
    /// <summary>Release notes shown in the changelog, with links to the guides.</summary>
    public ApiNote[] Notes { get; set; } = [];
    public int Major => ApiVersion.Major(Version);
}

/// <summary>One release note: <c>`code`</c>, <c>*emphasis*</c> and <c>[label](route)</c> become markup.</summary>
internal sealed class ApiNote
{
    public ApiEventKind Kind { get; set; }
    public string Text { get; set; } = "";
}

internal sealed class ApiChange
{
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
}

/// <summary>Version history shared by types and members.</summary>
internal abstract class ApiEntry
{
    /// <summary>Version that introduced the entry. Members without it share their type's version.</summary>
    public string? Since { get; set; }
    public string? DeprecatedIn { get; set; }
    /// <summary>Replacements (<c>Type</c> or <c>Type.Member</c>) suggested by the deprecation.</summary>
    public string[] ReplacedBy { get; set; } = [];
    /// <summary>Version that removed the entry; it may announce a future release.</summary>
    public string? RemovedIn { get; set; }
    public ApiChange[] Changes { get; set; } = [];

    public bool IsAvailableIn(string version, string since) =>
        ApiVersion.Compare(since, version) <= 0 && (RemovedIn is null || ApiVersion.Compare(version, RemovedIn) < 0);

    public bool IsDeprecatedIn(string version) => DeprecatedIn is not null && ApiVersion.Compare(DeprecatedIn, version) <= 0;
}

internal sealed class ApiType : ApiEntry
{
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Package { get; set; } = "";
    public string Kind { get; set; } = "";
    public string[] BaseTypes { get; set; } = [];
    public string Source { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Remarks { get; set; }
    public ApiMember[] Members { get; set; } = [];
    public ApiTypeDefinition[] EarlierDefinitions { get; set; } = [];
    public string Id => "api-" + Name.ToLowerInvariant();
    public ApiScope Scope => ApiReference.ScopeOf(Package);
    public string Introduced => Since ?? Scope.FirstVersion;

    public bool IsAvailableIn(string version) => IsAvailableIn(version, Introduced);

    public ApiType InVersion(string version)
    {
        var definition = EarlierDefinitions.FirstOrDefault(entry => ApiVersion.Compare(version, entry.Before) < 0);
        var result = (ApiType)MemberwiseClone();
        if (definition is not null)
        {
            result.Namespace = definition.Namespace ?? Namespace;
            result.Kind = definition.Kind ?? Kind;
            result.BaseTypes = definition.BaseTypes ?? BaseTypes;
            result.Summary = definition.Summary ?? Summary;
            result.Remarks = definition.Remarks ?? Remarks;
        }
        result.Members = (definition?.Members ?? Members).Select(member => member.InVersion(version)).ToArray();
        return result;
    }

    /// <summary>The C# declaration line, such as <c>public sealed class JsonFileStore : JsonTranslationStoreBase</c>.</summary>
    public string Declaration => $"public {Kind} {Name}" + (BaseTypes.Length > 0 ? " : " + string.Join(", ", BaseTypes) : "");

    /// <summary>Short badge style: class, interface or enum.</summary>
    public string KindStyle => Kind.Split(' ')[^1];
}

internal sealed class ApiMember : ApiEntry
{
    public string Signature { get; set; } = "";
    public string Description { get; set; } = "";
    public ApiMemberDefinition[] EarlierDefinitions { get; set; } = [];

    public string IntroducedWith(ApiType type) => Since ?? type.Introduced;

    public ApiMember InVersion(string version)
    {
        var definition = EarlierDefinitions.FirstOrDefault(entry => ApiVersion.Compare(version, entry.Before) < 0);
        if (definition is null) return this;
        var result = (ApiMember)MemberwiseClone();
        result.Signature = definition.Signature ?? Signature;
        result.Description = definition.Description ?? Description;
        return result;
    }
}

/// <summary>Overrides for releases before a breaking declaration or behavior change.</summary>
internal sealed class ApiTypeDefinition
{
    public string Before { get; set; } = "";
    public string? Namespace { get; set; }
    public string? Kind { get; set; }
    public string[]? BaseTypes { get; set; }
    public string? Summary { get; set; }
    public string? Remarks { get; set; }
    public ApiMember[]? Members { get; set; }
}

internal sealed class ApiMemberDefinition
{
    public string Before { get; set; } = "";
    public string? Signature { get; set; }
    public string? Description { get; set; }
}

internal enum ApiMemberGroup { Constructors, Properties, Methods, Operators, Values }

/// <summary>Kinds of change, in changelog order.</summary>
internal enum ApiEventKind { Added, Changed, Deprecated, Removed, Fixed }

/// <summary>Something that happened to a type or member in one version.</summary>
internal sealed record ApiEvent(ApiEventKind Kind, ApiType Type, ApiMember? Member, string? Description);

/// <summary>Semantic versions: numeric parts first, a prerelease sorts before its release.</summary>
internal static class ApiVersion
{
    public static int Compare(string left, string right)
    {
        var leftParts = left.Split('-', 2);
        var rightParts = right.Split('-', 2);
        var result = Version.Parse(leftParts[0]).CompareTo(Version.Parse(rightParts[0]));
        if (result != 0) return result;
        if (leftParts.Length != rightParts.Length) return leftParts.Length > rightParts.Length ? -1 : 1;
        return leftParts.Length == 1 ? 0 : string.CompareOrdinal(leftParts[1], rightParts[1]);
    }

    public static int Major(string version) => int.Parse(version.Split('.', 2)[0], System.Globalization.CultureInfo.InvariantCulture);
}

internal static partial class ApiReference
{
    private static readonly ApiCatalog Catalog = Load();
    public static readonly ApiScope[] Scopes = Catalog.Scopes;
    public static readonly ApiType[] Types = Catalog.Types;

    public static ApiScope Scope(string id) => Scopes.First(scope => scope.Id == id);

    public static ApiScope ScopeOf(string package) => Scopes.First(scope => scope.Packages.Contains(package));

    public static ApiType? FindById(string id) => Types.FirstOrDefault(type => type.Id == id);

    public static ApiType[] TypesIn(ApiScope scope, string version) => scope.Types.Where(type => type.IsAvailableIn(version))
        .Select(type => type.InVersion(version)).ToArray();

    private static readonly Dictionary<string, ApiType> TypesByName = Types.ToDictionary(type => type.Name);

    private static readonly HashSet<string> Keywords =
    [
        "public", "sealed", "abstract", "static", "class", "interface", "enum", "virtual", "override", "this",
        "string", "bool", "int", "long", "void", "object", "null", "default", "true", "false",
        "get", "set", "init", "implicit", "operator", "params", "out", "ref", "in", "new"
    ];

    public static ApiMemberGroup GroupOf(ApiType type, ApiMember member)
    {
        if (type.Kind == "enum") return ApiMemberGroup.Values;
        var signature = Modifiers().Replace(member.Signature, "");
        if (signature.StartsWith(type.Name + "(", StringComparison.Ordinal)) return ApiMemberGroup.Constructors;
        if (signature.Contains("operator ")) return ApiMemberGroup.Operators;
        if (signature.Contains("{ get")) return ApiMemberGroup.Properties;
        return ApiMemberGroup.Methods;
    }

    /// <summary>Additions, changes, deprecations and removals introduced by <paramref name="version"/> of <paramref name="scope"/>.</summary>
    public static IEnumerable<ApiEvent> EventsIn(ApiScope scope, string version)
    {
        bool Is(string? value) => value is not null && ApiVersion.Compare(value, version) == 0;
        var first = version == scope.FirstVersion;
        foreach (var type in scope.Types)
        {
            // Everything is new in the first version, so only later additions are listed.
            var typeAdded = !first && Is(type.Introduced);
            if (typeAdded) yield return new(ApiEventKind.Added, type, null, null);
            foreach (var change in type.Changes.Where(change => Is(change.Version))) yield return new(ApiEventKind.Changed, type, null, change.Description);
            if (Is(type.DeprecatedIn)) yield return new(ApiEventKind.Deprecated, type, null, null);
            if (Is(type.RemovedIn)) yield return new(ApiEventKind.Removed, type, null, null);
            foreach (var member in type.Members)
            {
                if (!typeAdded && !first && Is(member.Since)) yield return new(ApiEventKind.Added, type, member, null);
                foreach (var change in member.Changes.Where(change => Is(change.Version))) yield return new(ApiEventKind.Changed, type, member, change.Description);
                if (Is(member.DeprecatedIn)) yield return new(ApiEventKind.Deprecated, type, member, null);
                if (Is(member.RemovedIn)) yield return new(ApiEventKind.Removed, type, member, null);
            }
        }
    }

    /// <summary>Splits an enum entry such as <c>Upper = 1</c> into its name and its value, when declared explicitly.</summary>
    public static (string Name, string Value) EnumValue(ApiMember member)
    {
        var match = EnumEntry().Match(member.Signature);
        return match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : (member.Signature, "");
    }

    /// <summary>
    /// Renders C# code with token classes, links to documented types and, for long signatures,
    /// one parameter per line. <paramref name="link"/> gives the address of a documented type; without it, type names are not linked.
    /// </summary>
    public static MarkupString Highlight(string code, string? currentType = null, bool declaration = false, Func<ApiType, string>? link = null)
    {
        var tokens = Token().Matches(code).Select(match => match.Value).ToArray();
        var memberIndex = declaration ? Array.IndexOf(tokens, currentType) : MemberNameIndex(tokens);
        var wrap = code.Length > 72 && HasSeveralParameters(tokens);
        var html = new StringBuilder();
        int parens = 0, angles = 0;
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            var encoded = HtmlEncoder.Default.Encode(token);
            if (token == "(" && ++parens == 1 && wrap)
            {
                html.Append("<span class=\"tok-punct\">(</span>\n    ");
                continue;
            }
            if (token == ")") parens--;
            if (token == "<") angles++;
            if (token == ">") angles--;
            if (token == "," && parens == 1 && angles == 0 && wrap)
            {
                html.Append("<span class=\"tok-punct\">,</span>\n    ");
                if (index + 1 < tokens.Length && string.IsNullOrWhiteSpace(tokens[index + 1])) index++;
                continue;
            }

            if (index == memberIndex)
                html.Append("<span class=\"tok-member\">").Append(encoded).Append("</span>");
            else if (Keywords.Contains(token))
                html.Append("<span class=\"tok-keyword\">").Append(encoded).Append("</span>");
            else if (link is not null && TypesByName.TryGetValue(token, out var linked) && token != currentType)
                html.Append("<a class=\"tok-type\" href=\"").Append(HtmlEncoder.Default.Encode(link(linked))).Append("\">").Append(encoded).Append("</a>");
            else if (char.IsUpper(token[0]))
                html.Append("<span class=\"tok-type\">").Append(encoded).Append("</span>");
            else if (char.IsDigit(token[0]) || token[0] == '"')
                html.Append("<span class=\"tok-literal\">").Append(encoded).Append("</span>");
            else if (!char.IsLetter(token[0]) && token[0] != '_' && !char.IsWhiteSpace(token[0]))
                html.Append("<span class=\"tok-punct\">").Append(encoded).Append("</span>");
            else
                html.Append(encoded);
        }
        return new MarkupString(html.ToString());
    }

    /// <summary>The member's name, such as <c>WithCulture</c>, or the type name for a constructor.</summary>
    public static string MemberName(ApiMember member)
    {
        var tokens = Token().Matches(member.Signature).Select(match => match.Value).ToArray();
        var index = MemberNameIndex(tokens);
        return index >= 0 ? tokens[index] : member.Signature;
    }

    private static bool HasSeveralParameters(string[] tokens)
    {
        int parens = 0, angles = 0;
        foreach (var token in tokens)
        {
            if (token == "(") parens++;
            else if (token == ")") parens--;
            else if (token == "<") angles++;
            else if (token == ">") angles--;
            else if (token == "," && parens == 1 && angles == 0) return true;
        }
        return false;
    }

    // The member name is the last identifier before the first "(", "{" or "=".
    private static int MemberNameIndex(string[] tokens)
    {
        var candidate = -1;
        for (var index = 0; index < tokens.Length; index++)
        {
            var token = tokens[index];
            if (token is "(" or "{" or "=") break;
            if ((char.IsLetter(token[0]) || token[0] == '_') && !Keywords.Contains(token)) candidate = index;
        }
        return candidate;
    }

    private static ApiCatalog Load()
    {
        using var stream = typeof(ApiReference).Assembly.GetManifestResourceStream("FluentLocalizer.ApiReference.json")
            ?? throw new InvalidOperationException("The API reference catalog is missing.");
        var catalog = JsonSerializer.Deserialize(stream, ApiReferenceJsonContext.Default.ApiCatalog);
        return catalog is { Scopes.Length: > 0 } && catalog.Scopes.All(scope => scope.Versions.Length > 0)
            ? catalog : throw new InvalidOperationException("The API reference catalog is empty.");
    }

    [GeneratedRegex(@"\s+|[A-Za-z_][A-Za-z0-9_]*|\d+|""[^""]*""|.")]
    private static partial Regex Token();

    [GeneratedRegex(@"\b(static|virtual|override)\s+")]
    private static partial Regex Modifiers();

    [GeneratedRegex(@"^(\w+)\s*=\s*(.+)$")]
    private static partial Regex EnumEntry();
}

[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(ApiCatalog))]
internal partial class ApiReferenceJsonContext : JsonSerializerContext;
