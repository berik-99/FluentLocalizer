using FluentLocalizer;
using Microsoft.Extensions.DependencyInjection;

namespace FluentLocalizer.Test;

public class DependencyInjectionTests
{
    [Fact]
    public void AddFluentLocalizer_uses_the_supplied_options_instance()
    {
        var services = new ServiceCollection();
        services.AddFluentLocalizer(new TranslationOptions
        {
            MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
            MissingKeyFallbackValue = "missing:{key}"
        }).WithStore(new TestTranslationStore());

        using var provider = services.BuildServiceProvider();
        var translator = provider.GetRequiredService<ITranslator>();

        Assert.Equal("missing:welcome", translator.Get("welcome").Resolve());
    }
}
