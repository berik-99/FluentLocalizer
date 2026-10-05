using System.Reflection;
using System.Text.RegularExpressions;

namespace FluentLocalizer.Test;

public sealed class PublicApiReferenceTests
{
    [Fact]
    public void Catalog_covers_public_types_and_overloads_in_referenced_library_assemblies()
    {
        using var stream = typeof(PublicApiReferenceTests).Assembly.GetManifestResourceStream("FluentLocalizer.ApiReference.json");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream);
        var catalog = reader.ReadToEnd();
        // Only signature strings are needed here; descriptions stay in the shared JSON catalog.
        var headers = Regex.Matches(catalog, @"""Name"": ""(?<name>[^""]+)"",\s*""Namespace"": ""(?<ns>[^""]+)""");
        var documented = new Dictionary<string, string[]>();
        for (var index = 0; index < headers.Count; index++)
        {
            var header = headers[index];
            var end = index + 1 < headers.Count ? headers[index + 1].Index : catalog.Length;
            var block = catalog.Substring(header.Index, end - header.Index);
            documented.Add(header.Groups["ns"].Value + "." + header.Groups["name"].Value,
                Regex.Matches(block, @"""Signature"": ""(?<signature>[^""]+)""")
                    .Cast<Match>().Select(match => Normalize(match.Groups["signature"].Value)).ToArray());
        }

        var assemblies = typeof(PublicApiReferenceTests).Assembly.GetReferencedAssemblies()
            .Where(name => name.Name is "FluentLocalizer.Core" or "FluentLocalizer.Store.Json" or "FluentLocalizer.Extensions.DependencyInjection")
            .Select(Assembly.Load).ToArray();
        Assert.NotEmpty(assemblies);
        foreach (var type in assemblies.SelectMany(assembly => assembly.GetExportedTypes()))
        {
            Assert.True(documented.TryGetValue(type.FullName!, out var entries), $"Missing API type: {type.FullName}");
            var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
            var actual = type.GetConstructors(flags).Select(ctor => type.Name + Parameters(ctor))
                .Concat(type.GetMethods(flags).Where(method => !method.IsSpecialName || method.Name == "op_Implicit")
                    .Select(method => method.Name == "op_Implicit"
                        ? "implicit operator " + TypeName(method.ReturnType) + Parameters(method)
                        : TypeName(method.ReturnType) + " " + method.Name + Parameters(method)))
                .Concat(type.GetProperties(flags).Select(property => TypeName(property.PropertyType) + " " + property.Name))
                .Concat(type.GetFields(flags).Where(field => field.IsLiteral).Select(field => field.Name))
                .ToArray();
            foreach (var signature in actual)
                Assert.True(entries!.Contains(signature), $"Missing API member: {type.FullName}.{signature}");
        }
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

    private static string Normalize(string signature)
    {
        signature = signature.Replace("?", "");
        signature = Regex.Replace(signature, @"\b(static|virtual|override|this)\s+", "");
        signature = Regex.Replace(signature, @"\s*=\s*(null|default)(?=[,)])", "");
        signature = Regex.Replace(signature, @"\s*\{.*", "");
        signature = Regex.Replace(signature, @"\s*=\s*\d+$", "");
        return signature;
    }
}
