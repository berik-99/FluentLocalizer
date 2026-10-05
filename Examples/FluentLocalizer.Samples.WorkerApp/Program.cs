using FluentLocalizer;
using FluentLocalizer.Samples.WorkerApp;
using FluentLocalizer.Store.Json;
using System.Globalization;

// The sample speaks Italian: every translation request follows this UI culture.
CultureInfo culture = new("it-IT");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

TranslationOptions translationOptions = new()
{
    MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
    MissingKeyFallbackValue = "[missing:{key} in {culture}]",
    FormattingErrorBehavior = FormattingErrorBehavior.ReturnPlaceholder
};

JsonFileStoreOptions storeOptions = new()
{
    ResourcesPath = "Locales",
    ReloadOnChange = true,
    FallbackCulture = "en-US",
    ThrowOnMissingStore = true,
};

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddFluentLocalizer(translationOptions)
    .WithStore(new JsonFileStore(storeOptions))
    .WithLogger();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
