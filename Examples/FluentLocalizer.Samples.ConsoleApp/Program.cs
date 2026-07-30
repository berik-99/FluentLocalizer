using FluentLocalizer.Core;
using FluentLocalizer.Store.Json;
using System.Globalization;

CultureInfo culture = new("it-IT");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

TranslationOptions options = new()
{
    MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
    MissingKeyFallbackValue = "[missing:{key} in {culture}]",
    FormattingErrorBehavior = FormattingErrorBehavior.ThrowException,
    FormattingErrorExceptionFactory = (key, culture) => new TranslationException(
        key,
        culture,
        $"Formatting failed for '{key}' in '{culture?.Name ?? "unknown"}'."),
    DefaultArguments = new Dictionary<string, object?> { ["name"] = "Guest" }
};

JsonStoreOptions storeOptions = new()
{
    ResourcesPath = "Locales",
    SearchMode = JsonStoreLocation.FileSystem,
    ReloadOnChange = false,
    FallbackCulture = "en-US",
    ThrowOnError = true,
};

storeOptions.FileMappings.Add("en-US", "english.json");

Translator translator = new(new JsonStore(storeOptions), options);

await ShowScenarioAsync("1. Italian greeting", async () =>
    await translator.Get("Welcome")
        .WithArg("name", "Elena")
        .WithCulture("it-IT")
        .ResolveAsync());

await ShowScenarioAsync("2. Requesting German, Fallback to English", async () =>
    await translator.Get("Welcome")
        .WithArg("name", "Sofia")
        .WithCulture("de-DE")
        .ResolveAsync());

await ShowScenarioAsync("3. Nested notification with pluralization", async () =>
    await translator.Get("Notifications:MessageCount")
        .WithArg("name", "Elena")
        .Genderize(Gender.Female)
        .Pluralize(2)
        .WithCulture("it-IT")
        .ResolveAsync());

await ShowScenarioAsync("4. Default argument when runtime arg is missing", async () =>
    await translator.Get("Welcome")
        .WithCulture("en-US")
        .ResolveAsync());

await ShowScenarioAsync("5. Missing key fallback", async () =>
    await translator.Get("MissingGreeting")
        .WithCulture("it-IT")
        .ResolveAsync());

await ShowScenarioAsync("6. Formatting exception", async () =>
    await translator.Get("BrokenTemplate")
        .WithCulture("it-IT")
        .ResolveAsync());

static async Task ShowScenarioAsync(string title, Func<Task<string>> action)
{
    Console.WriteLine($"\n=== {title} ===");

    try
    {
        var result = await action();
        Console.WriteLine(result);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
    }
}