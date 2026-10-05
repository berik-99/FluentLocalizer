using System.Text.Json;
using System.Text.Json.Serialization;

namespace FluentLocalizer.WebApp;

internal sealed class ApiType
{
    public string Name { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Package { get; set; } = "";
    public string Source { get; set; } = "";
    public string Summary { get; set; } = "";
    public string? Remarks { get; set; }
    public ApiMember[] Members { get; set; } = [];
    public string Id => "api-" + Name.ToLowerInvariant();
}

internal sealed class ApiMember
{
    public string Signature { get; set; } = "";
    public string Description { get; set; } = "";
}

internal static class ApiReference
{
    public static readonly ApiType[] Types = Load();

    private static ApiType[] Load()
    {
        using var stream = typeof(ApiReference).Assembly.GetManifestResourceStream("FluentLocalizer.ApiReference.json")
            ?? throw new InvalidOperationException("The API reference catalog is missing.");
        return JsonSerializer.Deserialize(stream, ApiReferenceJsonContext.Default.ApiTypeArray)
            ?? throw new InvalidOperationException("The API reference catalog is empty.");
    }
}

[JsonSerializable(typeof(ApiType[]))]
internal partial class ApiReferenceJsonContext : JsonSerializerContext;
