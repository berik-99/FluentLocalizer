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
Console.WriteLine(translator.Get("Hello").Resolve()); // No WithCulture needed: CurrentUICulture is it-IT.
Console.WriteLine(translator.Get("Hello").WithCulture("de-DE").Resolve()); // Forces German for this request only; MemoryStore falls back to English.

Console.WriteLine("\n=== Advanced: arguments, plural, fallback, and error policy ===");
await ShowScenarioAsync("Italian greeting with a runtime argument", async () =>
    await translator.Get("Welcome")
        .WithArg("name", "Elena")
        .ResolveAsync());

await ShowScenarioAsync("Forced German request falls back to English", async () =>
    await translator.Get("Welcome")
        .WithArg("name", "Sofia")
        .WithCulture("de-DE") // Overrides CurrentUICulture for this request only.
        .ResolveAsync());

await ShowScenarioAsync("Nested key, gender and plural", async () =>
    await translator.Get("Notifications:MessageCount")
        .WithArg("name", "Elena")
        .Genderize(Gender.Female)
        .Pluralize(2)
        .ResolveAsync());

await ShowScenarioAsync("Default argument when runtime arg is missing", async () =>
    await translator.Get("Welcome")
        .ResolveAsync());

await ShowScenarioAsync("Missing key fallback", async () =>
    await translator.Get("MissingGreeting")
        .ResolveAsync());

await ShowScenarioAsync("Case transformation", () =>
    Task.FromResult(translator.Get("Hello").WithCase(LetterCase.Upper).Resolve()));

await ShowScenarioAsync("Custom formatting exception", async () =>
    await translator.Get("BrokenTemplate")
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
