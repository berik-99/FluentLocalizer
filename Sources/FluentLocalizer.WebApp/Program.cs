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

builder.Services.AddSingleton<ITranslationStore>(new EmbeddedJsonStore(new EmbeddedJsonStoreOptions
{
    ResourceAssembly = typeof(SiteText).Assembly,
    ResourcesPath = "Locales",
    FallbackCulture = "en",
    ThrowOnMissingStore = true
}));
builder.Services.AddSingleton<ITranslator, Translator>();
builder.Services.AddSingleton<SiteText>();

var host = builder.Build();
var js = host.Services.GetRequiredService<IJSRuntime>();
var text = host.Services.GetRequiredService<SiteText>();
text.Culture = SiteText.SelectCulture(await js.InvokeAsync<string[]>("fluentSite.getLanguages"));
CultureInfo.DefaultThreadCurrentCulture = text.Culture;
CultureInfo.DefaultThreadCurrentUICulture = text.Culture;
await js.InvokeVoidAsync("fluentSite.setLanguage", text.Culture.Name, text.BrowserLabels);
await host.RunAsync();
