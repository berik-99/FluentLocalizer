using FluentLocalizer;
using FluentLocalizer.Samples.WorkerApp;
using FluentLocalizer.Store.Json;

TranslationOptions translationOptions = new()
{
    MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
    MissingKeyFallbackValue = "[missing:{key} in {culture}]",
    FormattingErrorBehavior = FormattingErrorBehavior.ReturnPlaceholder,
    FormattingErrorExceptionFactory = (key, culture) => new TranslationException(
        key,
        culture,
        $"Formatting failed for '{key}' in '{culture?.Name ?? "unknown"}'."),
    DefaultArguments = new Dictionary<string, object?> { ["name"] = "Guest" }
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
    .WithStore(new JsonFileStore(storeOptions));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
