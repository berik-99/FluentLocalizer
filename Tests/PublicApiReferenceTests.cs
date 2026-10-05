using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FluentLocalizer.Test;

public sealed class PublicApiReferenceTests
{
    private static readonly string[] LibraryAssemblies =
        ["FluentLocalizer.Core", "FluentLocalizer.Store.Json", "FluentLocalizer.Extensions.DependencyInjection"];

    [Fact]
    public void Catalog_covers_public_types_and_overloads_in_referenced_library_assemblies()
    {
        using var catalog = LoadCatalog();
        var assemblies = LoadedLibraries();
        Assert.NotEmpty(assemblies);
        var documented = catalog.RootElement.GetProperty("Types").EnumerateArray()
            .ToDictionary(type => type.GetProperty("Namespace").GetString() + "." + type.GetProperty("Name").GetString());

        foreach (var type in assemblies.SelectMany(assembly => assembly.GetExportedTypes()))
        {
            Assert.True(documented.TryGetValue(type.FullName!, out var entry), $"Missing API type: {type.FullName}");
            Assert.Equal(Declaration(Kind(type), BaseTypes(type)),
                Declaration(entry.GetProperty("Kind").GetString()!, entry.GetProperty("BaseTypes").EnumerateArray().Select(name => name.GetString()!)));
            Assert.True(type.IsDefined(typeof(ObsoleteAttribute), false) == entry.TryGetProperty("DeprecatedIn", out _),
                $"[Obsolete] and DeprecatedIn disagree for {type.FullName}");
            Assert.False(IsRemoved(entry, CurrentVersion(type.Assembly)), $"{type.FullName} is marked as removed but still exists");

            var members = entry.GetProperty("Members").EnumerateArray()
                .ToDictionary(member => Normalize(member.GetProperty("Signature").GetString()!), member => member);
            foreach (var (signature, member) in ActualMembers(type))
            {
                Assert.True(members.TryGetValue(signature, out var documentedMember), $"Missing API member: {type.FullName}.{signature}");
                Assert.True(member.IsDefined(typeof(ObsoleteAttribute), false) == documentedMember.TryGetProperty("DeprecatedIn", out _),
                    $"[Obsolete] and DeprecatedIn disagree for {type.FullName}.{signature}");
                Assert.False(IsRemoved(documentedMember, CurrentVersion(type.Assembly)), $"{type.FullName}.{signature} is marked as removed but still exists");
            }

            var availableSignatures = ActualMembers(type, declaredOnly: false).Select(member => member.Signature).ToHashSet();
            foreach (var member in members)
            {
                if (IsRemoved(member.Value, CurrentVersion(type.Assembly))) continue;
                Assert.Contains(member.Key, availableSignatures);
            }

            // Enum values are documented only when the source declares them explicitly; when present they must match.
            if (type.IsEnum)
            {
                foreach (var member in entry.GetProperty("Members").EnumerateArray())
                {
                    var explicitValue = Regex.Match(member.GetProperty("Signature").GetString()!, @"^(?<name>\w+)\s*=\s*(?<value>-?\d+)$");
                    if (explicitValue.Success)
                        Assert.Equal(explicitValue.Groups["value"].Value,
                            Convert.ToInt64(Enum.Parse(type, explicitValue.Groups["name"].Value)).ToString());
                }
            }
        }

        // Documented types that are not marked as removed must still ship in their assembly.
        foreach (var entry in documented)
        {
            var assembly = assemblies.FirstOrDefault(candidate => candidate.GetName().Name == AssemblyOf(entry.Value));
            if (assembly is null || IsRemoved(entry.Value, CurrentVersion(assembly))) continue;
            Assert.True(assembly.GetType(entry.Key) is not null, $"Documented API type no longer exists: {entry.Key}. Set RemovedIn instead of deleting it.");
        }
    }

    [Fact]
    public void Catalog_versions_are_consistent()
    {
        using var catalog = LoadCatalog();
        var releases = catalog.RootElement.GetProperty("Versions").EnumerateArray()
            .Select(release => release.GetProperty("Version").GetString()!).ToArray();
        Assert.NotEmpty(releases);
        Assert.All(releases, AssertSemVer);
        for (var index = 1; index < releases.Length; index++)
            Assert.True(CompareVersions(releases[index - 1], releases[index]) < 0, $"Versions must be listed in ascending order: {releases[index]}");
        foreach (var assembly in LoadedLibraries())
            Assert.Contains(CurrentVersion(assembly), releases);

        var types = catalog.RootElement.GetProperty("Types").EnumerateArray().ToArray();
        var typeNames = types.Select(type => type.GetProperty("Name").GetString()!).ToHashSet();
        foreach (var type in types)
        {
            var name = type.GetProperty("Name").GetString()!;
            Assert.True(type.TryGetProperty("Since", out var since), $"{name} needs Since");
            AssertLifecycle(name, type, since.GetString()!, releases, typeNames);
            AssertEarlierDefinitions(name, type, since.GetString()!, releases);
            foreach (var member in type.GetProperty("Members").EnumerateArray())
            {
                var memberName = name + "." + member.GetProperty("Signature").GetString();
                var memberSince = member.TryGetProperty("Since", out var value) ? value.GetString()! : since.GetString()!;
                Assert.True(CompareVersions(since.GetString()!, memberSince) <= 0, $"{memberName} cannot predate its type");
                AssertLifecycle(memberName, member, memberSince, releases, typeNames);
                AssertEarlierDefinitions(memberName, member, memberSince, releases);
            }
        }
    }

    private static void AssertEarlierDefinitions(string name, JsonElement entry, string since, string[] releases)
    {
        if (!entry.TryGetProperty("EarlierDefinitions", out var definitions)) return;
        string? previous = null;
        foreach (var definition in definitions.EnumerateArray())
        {
            var before = definition.GetProperty("Before").GetString()!;
            Assert.Contains(before, releases);
            Assert.True(CompareVersions(since, before) < 0, $"{name}: EarlierDefinitions must end after introduction");
            if (previous is not null)
                Assert.True(CompareVersions(previous, before) < 0, $"{name}: EarlierDefinitions must be in ascending order");
            previous = before;
        }
    }

    private static void AssertLifecycle(string name, JsonElement entry, string since, string[] releases, HashSet<string> typeNames)
    {
        Assert.True(releases.Contains(since), $"{name}: Since {since} is not listed in Versions");
        if (entry.TryGetProperty("DeprecatedIn", out var deprecated))
        {
            Assert.True(releases.Contains(deprecated.GetString()), $"{name}: DeprecatedIn is not listed in Versions");
            Assert.True(CompareVersions(since, deprecated.GetString()!) <= 0, $"{name}: deprecated before it was introduced");
        }
        if (entry.TryGetProperty("RemovedIn", out var removed))
        {
            // RemovedIn may announce a future release, so it only has to be a valid version after the deprecation.
            AssertSemVer(removed.GetString()!);
            Assert.True(deprecated.ValueKind == JsonValueKind.String, $"{name}: deprecate an API before removing it");
            Assert.True(CompareVersions(deprecated.GetString()!, removed.GetString()!) < 0, $"{name}: RemovedIn must follow DeprecatedIn");
        }
        if (entry.TryGetProperty("ReplacedBy", out var replacements))
        {
            Assert.True(deprecated.ValueKind == JsonValueKind.String, $"{name}: ReplacedBy requires DeprecatedIn");
            foreach (var replacement in replacements.EnumerateArray())
                Assert.True(typeNames.Contains(replacement.GetString()!.Split('.')[0]), $"{name}: unknown replacement {replacement}");
        }
        if (entry.TryGetProperty("Changes", out var changes))
        {
            foreach (var change in changes.EnumerateArray())
            {
                var version = change.GetProperty("Version").GetString()!;
                Assert.True(releases.Contains(version), $"{name}: change version {version} is not listed in Versions");
                Assert.True(CompareVersions(since, version) < 0, $"{name}: a change must follow the version that introduced the API");
                Assert.False(string.IsNullOrWhiteSpace(change.GetProperty("Description").GetString()));
            }
        }
    }

    private static JsonDocument LoadCatalog()
    {
        using var stream = typeof(PublicApiReferenceTests).Assembly.GetManifestResourceStream("FluentLocalizer.ApiReference.json");
        Assert.NotNull(stream);
        return JsonDocument.Parse(stream);
    }

    private static Assembly[] LoadedLibraries() =>
        typeof(PublicApiReferenceTests).Assembly.GetReferencedAssemblies()
            .Where(name => LibraryAssemblies.Contains(name.Name))
            .Select(Assembly.Load).ToArray();

    // The project folder in Source matches the assembly name, such as Sources/FluentLocalizer.Core/Translator.cs.
    private static string AssemblyOf(JsonElement type) => type.GetProperty("Source").GetString()!.Split('/')[1];

    private static string CurrentVersion(Assembly assembly) =>
        (assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version!.ToString(3)).Split('+')[0];

    private static bool IsRemoved(JsonElement entry, string currentVersion) =>
        entry.TryGetProperty("RemovedIn", out var removed) && CompareVersions(removed.GetString()!, currentVersion) <= 0;

    private static void AssertSemVer(string version) =>
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", version);

    // Numeric parts first; a prerelease sorts before the release with the same number.
    private static int CompareVersions(string left, string right)
    {
        var leftParts = left.Split(['-'], 2);
        var rightParts = right.Split(['-'], 2);
        var result = new Version(leftParts[0]).CompareTo(new Version(rightParts[0]));
        if (result != 0) return result;
        if (leftParts.Length != rightParts.Length) return leftParts.Length > rightParts.Length ? -1 : 1;
        return leftParts.Length == 1 ? 0 : string.CompareOrdinal(leftParts[1], rightParts[1]);
    }

    private static IEnumerable<(string Signature, MemberInfo Member)> ActualMembers(Type type, bool declaredOnly = true)
    {
        var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
        if (declaredOnly) flags |= BindingFlags.DeclaredOnly;
        return type.GetConstructors(flags).Select(ctor => (type.Name + Parameters(ctor), (MemberInfo)ctor))
            .Concat(type.GetMethods(flags).Where(method => !method.IsSpecialName || method.Name == "op_Implicit")
                .Select(method => (method.Name == "op_Implicit"
                    ? "implicit operator " + TypeName(method.ReturnType) + Parameters(method)
                    : TypeName(method.ReturnType) + " " + method.Name + Parameters(method), (MemberInfo)method)))
            .Concat(type.GetProperties(flags).Select(property => (TypeName(property.PropertyType) + " " + property.Name, (MemberInfo)property)))
            .Concat(type.GetFields(flags).Where(field => field.IsLiteral).Select(field => (field.Name, (MemberInfo)field)));
    }

    // Kind and direct base types (in any order), compared as one readable string.
    private static string Declaration(string kind, IEnumerable<string> baseTypes) =>
        $"{kind} : {string.Join(", ", baseTypes.OrderBy(name => name, StringComparer.Ordinal))}";

    private static string Kind(Type type) =>
        type.IsInterface ? "interface"
        : type.IsEnum ? "enum"
        : type.IsAbstract && type.IsSealed ? "static class"
        : type.IsAbstract ? "abstract class"
        : type.IsSealed ? "sealed class"
        : "class";

    private static IEnumerable<string> BaseTypes(Type type)
    {
        var inherited = type.BaseType?.GetInterfaces() ?? [];
        var interfaces = type.GetInterfaces().Except(inherited)
            .Where(candidate => !type.GetInterfaces().Any(other => other != candidate && candidate.IsAssignableFrom(other)));
        Type[] baseType = type.BaseType is { } parent && parent != typeof(object) && parent != typeof(Enum) ? [parent] : [];
        return baseType.Concat(interfaces).Select(TypeName);
    }

    private static string Parameters(MethodBase method) =>
        "(" + string.Join(", ", method.GetParameters().Select(parameter => TypeName(parameter.ParameterType) + " " + parameter.Name)) + ")";

    private static string TypeName(Type type)
    {
        if (type == typeof(void)) return "void";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "object";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        return type.IsGenericType
            ? type.Name.Split('`')[0] + "<" + string.Join(", ", type.GetGenericArguments().Select(TypeName)) + ">"
            : type.Name;
    }

    internal static string Normalize(string signature)
    {
        signature = signature.Replace("?", "");
        signature = Regex.Replace(signature, @"\b(static|virtual|override|this)\s+", "");
        signature = Regex.Replace(signature, @"\s*=\s*(null|default)(?=[,)])", "");
        signature = Regex.Replace(signature, @"\s*\{.*", "");
        signature = Regex.Replace(signature, @"\s*=\s*-?\d+$", "");
        return signature;
    }
}
