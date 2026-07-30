using FluentLocalizer.Core;
using System.Globalization;

namespace FluentLocalizer.Test;

public class TranslationOptionsTests
{
    [Fact]
    public void CreateArgumentSet_merges_defaults_and_runtime_arguments()
    {
        TranslationOptions options = new()
        {
            DefaultArguments = new Dictionary<string, object?>
            {
                ["name"] = "Ada",
                ["city"] = "London"
            }
        };

        var arguments = options.CreateArgumentSet(new Dictionary<string, object?>
        {
            ["name"] = "Grace"
        });

        Assert.Equal("Grace", arguments["name"]);
        Assert.Equal("London", arguments["city"]);
        Assert.Equal(2, arguments.Count);
    }

    [Fact]
    public void CreateMissingKeyException_uses_custom_factory_when_available()
    {
        TranslationOptions options = new()
        {
            MissingKeyExceptionFactory = (key, culture) => new TranslationException(key, culture, $"custom {key}")
        };

        var exception = Assert.IsType<TranslationException>(options.CreateMissingKeyException("welcome", new CultureInfo("fr-FR")));

        Assert.Equal("custom welcome", exception.Message);
        Assert.Equal("welcome", exception.Key);
        Assert.Equal("fr-FR", exception.Culture?.Name);
    }

    [Fact]
    public void CreateFormattingException_uses_custom_factory_when_available()
    {
        TranslationOptions options = new()
        {
            FormattingErrorExceptionFactory = (key, culture) => new TranslationException(key, culture, $"formatting {key}")
        };

        var exception = Assert.IsType<TranslationException>(options.CreateFormattingException("welcome", new CultureInfo("it-IT"), new InvalidOperationException("boom")));

        Assert.Equal("formatting welcome", exception.Message);
        Assert.Equal("welcome", exception.Key);
        Assert.Equal("it-IT", exception.Culture?.Name);
    }
}
