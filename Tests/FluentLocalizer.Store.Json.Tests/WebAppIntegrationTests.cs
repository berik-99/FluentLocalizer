#if NET10_0
using System.Text.Json;
using FluentLocalizer.WebApp;
using Microsoft.Extensions.DependencyInjection;

namespace FluentLocalizer.Test;

public sealed class WebAppIntegrationTests
{
    [Fact]
    public void Playground_sessions_use_DI_and_isolate_templates_and_strict_options()
    {
        var services = new ServiceCollection();
        services.AddFluentLocalizer().WithStore(new PlaygroundStore { Template = "Site" });
        services.AddTransient<PlaygroundSession>();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<PlaygroundSession>();
        var second = secondScope.ServiceProvider.GetRequiredService<PlaygroundSession>();

        first.Store.Template = "Hello, {name}!";
        second.Store.Template = "Second";
        Assert.Equal("Hello, Ada!", first.Translator.Get("playground").WithArg("name", "Ada").Resolve());
        Assert.Equal("Second", second.Translator.Get("playground").Resolve());
        Assert.Equal("Site", provider.GetRequiredService<ITranslator>().Get("playground").Resolve());
        Assert.Equal("[missing]", provider.GetRequiredService<ITranslator>().Get("missing").Resolve());
        Assert.Throws<TranslationException>(() => first.Translator.Get("missing").Resolve());
        first.Store.Template = "{quantity, plural, one{item}";
        Assert.Throws<TranslationException>(() => first.Translator.Get("playground").Resolve());
    }

    [Fact]
    public void Initial_reference_matches_public_API_snapshot_from_published_0_0_1_packages()
    {
        using var stream = typeof(WebAppIntegrationTests).Assembly.GetManifestResourceStream("FluentLocalizer.ApiSnapshot.0.0.1.json");
        Assert.NotNull(stream);
        using var snapshot = JsonDocument.Parse(stream);
        var actual = ApiReference.TypesIn("0.0.1").OrderBy(type => type.Name).ToArray();
        var expected = snapshot.RootElement.GetProperty("Types").EnumerateArray().OrderBy(type => type.GetProperty("Name").GetString()).ToArray();
        Assert.Equal(expected.Length, actual.Length);
        for (var index = 0; index < expected.Length; index++)
        {
            var entry = expected[index];
            var type = actual[index];
            Assert.Equal(entry.GetProperty("Name").GetString(), type.Name);
            Assert.Equal(entry.GetProperty("Namespace").GetString(), type.Namespace);
            Assert.Equal(entry.GetProperty("Kind").GetString(), type.Kind);
            Assert.Equal(entry.GetProperty("BaseTypes").EnumerateArray().Select(value => value.GetString()).OrderBy(value => value), type.BaseTypes.OrderBy(value => value));
            Assert.False(type.IsDeprecatedIn("0.0.1"));
            Assert.Equal(entry.GetProperty("Members").EnumerateArray().Select(value => value.GetString()).OrderBy(value => value),
                type.Members.Where(member => member.IsAvailableIn("0.0.1", member.IntroducedWith(type))).Select(member => PublicApiReferenceTests.Normalize(member.Signature)).OrderBy(value => value));
        }
    }

    [Fact]
    public void History_renders_old_options_and_preserves_latest_definitions()
    {
        var initial = ApiReference.TypesIn("0.0.1");
        var stable = ApiReference.TypesIn("1.0.0");
        var alpha = ApiReference.TypesIn("0.1.0-alpha");
        Assert.Equal(25, alpha.Length);
        Assert.Equal(alpha.Select(type => type.Declaration), stable.Select(type => type.Declaration));
        Assert.DoesNotContain(initial, type => type.Name == "HttpJsonStore");
        Assert.Contains(initial.Single(type => type.Name == "TranslationOptions").Members, member => member.Signature.Contains("get; init;"));
        Assert.DoesNotContain(stable.Single(type => type.Name == "TranslationOptions").Members, member => member.Signature.Contains("init;"));
        Assert.Contains(initial.Single(type => type.Name == "JsonStoreOptions").Members, member => member.Signature.Contains("ThrowOnError"));
        Assert.DoesNotContain(stable.Single(type => type.Name == "JsonStoreOptions").Members, member => member.Signature.Contains("ThrowOnError"));
        Assert.Contains(alpha, type => type.Name == "JsonStore" && type.IsDeprecatedIn("0.1.0-alpha"));
        Assert.Equal("1.0.0", ApiReference.LatestRelease);
        Assert.Equal("0.0.1", ApiReference.FirstVersion);
        Assert.Empty(ApiReference.EventsIn("1.0.0"));
        Assert.Equal(8, ApiReference.EventsIn("0.1.0-alpha").Count(change => change.Kind == ApiEventKind.Added && change.Member is null));
    }
}
#endif
