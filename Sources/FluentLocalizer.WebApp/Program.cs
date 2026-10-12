using System.Globalization;
using FluentLocalizer;
using FluentLocalizer.Store.Json;
using FluentLocalizer.WebApp;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddFluentLocalizer().WithStore(_ => new EmbeddedJsonStore(new EmbeddedJsonStoreOptions
{
    ResourceAssembly = typeof(App).Assembly,
    ResourcesPath = "Locales",
    FallbackCulture = "en",
    ThrowOnMissingStore = true,
    // Only the visitor's language is parsed, and its Docs namespace file only on documentation pages.
    LoadOnDemand = true
}));
builder.Services.AddTransient<PlaygroundSession>();

var host = builder.Build();
var js = host.Services.GetRequiredService<IJSRuntime>();
var culture = BrowserCulture.SelectCulture(await js.InvokeAsync<string[]>("fluentSite.getLanguages"));
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
var translator = host.Services.GetRequiredService<ITranslator>();
await js.InvokeVoidAsync("fluentSite.setLanguage", culture.Name, new
{
    loading = translator.Get("Ui:Loading").Resolve(),
    errorTitle = translator.Get("Ui:ErrorTitle").Resolve(),
    error = translator.Get("Ui:UnhandledError").Resolve(),
    reload = translator.Get("Ui:Reload").Resolve(),
    dismiss = translator.Get("Ui:Dismiss").Resolve()
});
await host.RunAsync();
