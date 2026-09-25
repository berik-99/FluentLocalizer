using FluentLocalizer;
using FluentLocalizer.Store.Http;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

var httpClient = new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
};

var store = new HttpJsonStore(httpClient, new HttpJsonStoreOptions
{
    ResourcesPath = "/locales",
    FallbackCulture = "en-US",
    ThrowOnMissingStore = true
});

builder.Services.AddSingleton(store);
builder.Services.AddFluentLocalizer(options =>
{
    options.MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue;
    options.MissingKeyFallbackValue = "[missing:{key} in {culture}]";
})
.WithStore(store);

var app = builder.Build();

// Download all supported locales once when the WebAssembly client starts.
await store.LoadAsync(["en-US", "it-IT"]);

await app.RunAsync();
