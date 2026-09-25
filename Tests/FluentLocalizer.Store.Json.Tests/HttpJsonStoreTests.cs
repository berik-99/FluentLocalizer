using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using FluentLocalizer.Store.Json;

namespace FluentLocalizer.Test;

public sealed class HttpJsonStoreTests
{
    [Fact]
    public async Task Loads_on_demand_caches_documents_and_supports_sync_after_load()
    {
        var handler = new StubHttpHandler((request, _) => JsonResponse(request.RequestUri!.AbsolutePath switch
        {
            "/translations/it-IT.json" => "{\"Hello\":\"Ciao\"}",
            _ => null
        }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client, new HttpJsonStoreOptions { ResourcesPath = "translations" });

        Assert.Equal("Ciao", await store.GetTemplateAsync("Hello", new CultureInfo("it-IT"), TestContext.Current.CancellationToken));
        Assert.Equal("Ciao", store.GetTemplate("Hello", new CultureInfo("it-IT")));
        Assert.Equal(1, handler.Requests.Count(request => request.EndsWith("/it-IT.json", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task LoadAsync_preloads_cultures_and_fallback_then_supports_sync_lookup()
    {
        var handler = new StubHttpHandler((request, _) => JsonResponse(request.RequestUri!.AbsolutePath switch
        {
            "/locales/fr-FR.json" => "{\"Hello\":\"Bonjour\"}",
            "/locales/en-US.json" => "{\"Hello\":\"Hello\",\"OnlyEnglish\":\"Fallback\"}",
            _ => null
        }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client);

        await store.LoadAsync(["fr-FR", "FR-fr"], TestContext.Current.CancellationToken);

        Assert.Equal("Bonjour", store.GetTemplate("Hello", new CultureInfo("fr-FR")));
        Assert.Equal("Fallback", store.GetTemplate("OnlyEnglish", new CultureInfo("fr-FR")));
        Assert.Equal(1, handler.Requests.Count(request => request.EndsWith("/fr-FR.json", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Supports_nested_lookup_file_mappings_and_escaped_paths()
    {
        var handler = new StubHttpHandler((request, _) => JsonResponse(request.RequestUri!.AbsolutePath switch
        {
            "/assets%20locales/italiano%20speciale.json" => "{\"Home\":{\"Welcome\":\"Benvenuto\"}}",
            _ => null
        }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var options = new HttpJsonStoreOptions { ResourcesPath = "assets locales" };
        options.FileMappings["it-IT"] = "italiano speciale.json";
        using var store = new HttpJsonStore(client, options);

        Assert.Equal("Benvenuto", await store.GetTemplateAsync("Home:Welcome", new CultureInfo("it-IT"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Missing_files_return_null_or_throw_according_to_option()
    {
        using var client = new HttpClient(new StubHttpHandler((_, _) => JsonResponse(null)))
        {
            BaseAddress = new Uri("https://example.test/")
        };
        using var quiet = new HttpJsonStore(client);
        Assert.Null(await quiet.GetTemplateAsync("Missing", new CultureInfo("it-IT"), TestContext.Current.CancellationToken));

        using var strict = new HttpJsonStore(client, new HttpJsonStoreOptions { ThrowOnMissingStore = true });
        await Assert.ThrowsAsync<FileNotFoundException>(() => strict.GetTemplateAsync("Missing", new CultureInfo("it-IT"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Non_success_responses_invalid_json_and_cancellation_propagate()
    {
        using var errorClient = new HttpClient(new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError))))
        { BaseAddress = new Uri("https://example.test/") };
        using var errorStore = new HttpJsonStore(errorClient);
        await Assert.ThrowsAsync<HttpRequestException>(() => errorStore.GetTemplateAsync("Hello", new CultureInfo("it-IT"), TestContext.Current.CancellationToken));

        using var jsonClient = new HttpClient(new StubHttpHandler((_, _) => JsonResponse("{invalid")))
        { BaseAddress = new Uri("https://example.test/") };
        using var jsonStore = new HttpJsonStore(jsonClient);
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => jsonStore.GetTemplateAsync("Hello", new CultureInfo("it-IT"), TestContext.Current.CancellationToken));

        using var blockingClient = new HttpClient(new StubHttpHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return CreateJsonResponse(null);
        })) { BaseAddress = new Uri("https://example.test/") };
        using var blockingStore = new HttpJsonStore(blockingClient);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => blockingStore.GetTemplateAsync("Hello", new CultureInfo("it-IT"), cancellation.Token));
    }

    [Fact]
    public async Task Refresh_replaces_cached_content_and_can_remove_a_file()
    {
        var content = "{\"Value\":\"before\"}";
        var handler = new StubHttpHandler((_, _) => JsonResponse(content));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client);
        var culture = new CultureInfo("it-IT");

        await store.LoadAsync(culture, TestContext.Current.CancellationToken);
        Assert.Equal("before", store.GetTemplate("Value", culture));
        content = "{\"Value\":\"after\"}";
        await store.RefreshStoreAsync(TestContext.Current.CancellationToken);
        Assert.Equal("after", store.GetTemplate("Value", culture));

        handler.Respond = (_, _) => JsonResponse(null);
        await store.RefreshStoreAsync(TestContext.Current.CancellationToken);
        Assert.Null(store.GetTemplate("Value", culture));
    }

    private static HttpResponseMessage CreateJsonResponse(string? json) => json is null
        ? new HttpResponseMessage(HttpStatusCode.NotFound)
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsolutePath);
            return Respond(request, cancellationToken);
        }
    }

    private static Task<HttpResponseMessage> JsonResponse(string? json) => Task.FromResult(CreateJsonResponse(json));
}
