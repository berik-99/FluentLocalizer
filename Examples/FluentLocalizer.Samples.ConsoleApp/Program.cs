using FluentLocalizer;
using FluentLocalizer.Samples.ConsoleApp;
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

Translator translator = new(new MemoryStore(), options);

Console.WriteLine("=== Base: custom ITranslationStore and synchronous lookup ===");
Console.WriteLine(translator.Get("Hello").Resolve()); // CurrentUICulture is it-IT.
Console.WriteLine(translator.Get("Hello").WithCulture("de-DE").Resolve()); // MemoryStore falls back to English.

Console.WriteLine("\n=== Advanced: arguments, plural, fallback, and error policy ===");
await ShowScenarioAsync("Italian greeting with a runtime argument", async () =>
    await translator.Get("Welcome")
        .WithArg("name", "Elena")
        .WithCulture("it-IT")
        .ResolveAsync());

await ShowScenarioAsync("German request falls back to English", async () =>
    await translator.Get("Welcome")
        .WithArg("name", "Sofia")
        .WithCulture("de-DE")
        .ResolveAsync());

await ShowScenarioAsync("Nested key, gender and plural", async () =>
    await translator.Get("Notifications:MessageCount")
        .WithArg("name", "Elena")
        .Genderize(Gender.Female)
        .Pluralize(2)
        .WithCulture("it-IT")
        .ResolveAsync());

await ShowScenarioAsync("Default argument when runtime arg is missing", async () =>
    await translator.Get("Welcome")
        .WithCulture("en-US")
        .ResolveAsync());

await ShowScenarioAsync("Missing key fallback", async () =>
    await translator.Get("MissingGreeting")
        .WithCulture("it-IT")
        .ResolveAsync());

await ShowScenarioAsync("Case transformation", () =>
    Task.FromResult(translator.Get("Hello").WithCulture("en-US").WithCase(LetterCase.Upper).Resolve()));

await ShowScenarioAsync("Custom formatting exception", async () =>
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
