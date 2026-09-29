using System.Globalization;
using System.Collections.Concurrent;
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
    public async Task Strict_mode_accepts_a_fallback_file_when_requested_culture_is_missing()
    {
        var handler = new StubHttpHandler((request, _) => JsonResponse(request.RequestUri!.AbsolutePath.EndsWith("/en-US.json", StringComparison.Ordinal)
            ? "{\"Hello\":\"Fallback\"}" : null));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client, new HttpJsonStoreOptions { ThrowOnMissingStore = true });

        Assert.Equal("Fallback", await store.GetTemplateAsync("Hello", new CultureInfo("it-IT"), TestContext.Current.CancellationToken));
        await store.LoadAsync(new CultureInfo("it-IT"), TestContext.Current.CancellationToken);
        Assert.Equal("Fallback", store.GetTemplate("Hello", new CultureInfo("it-IT")));
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

    [Fact]
    public async Task Invalid_refresh_preserves_the_loaded_snapshot()
    {
        var content = "{\"Value\":\"before\"}";
        using var client = new HttpClient(new StubHttpHandler((_, _) => JsonResponse(content)))
        { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client);
        var culture = new CultureInfo("it-IT");
        await store.LoadAsync(culture, TestContext.Current.CancellationToken);

        content = "{invalid";
        await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => store.RefreshStoreAsync(TestContext.Current.CancellationToken));
        Assert.Equal("before", store.GetTemplate("Value", culture));
    }

    [Fact]
    public async Task Namespaced_lazy_lookup_loads_only_the_matching_bundle()
    {
        var handler = new StubHttpHandler((request, _) => JsonResponse(request.RequestUri!.AbsolutePath switch
        {
            "/locales/it-IT.common.json" => "{\"Title\":\"Titolo\"}",
            _ => null
        }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client);
        var culture = new CultureInfo("it-IT");

        Assert.Equal("Titolo", await store.GetTemplateAsync("common:Title", culture, TestContext.Current.CancellationToken));
        var firstRequestCount = handler.Requests.Count;
        Assert.Equal("Titolo", await store.GetTemplateAsync("common:Title", culture, TestContext.Current.CancellationToken));
        Assert.Equal(firstRequestCount, handler.Requests.Count);
        Assert.Equal("Titolo", store.GetTemplate("common:Title", culture));
        Assert.DoesNotContain(handler.Requests, path => path.EndsWith("/it-IT.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Manifest_preloads_namespaces_and_selects_the_first_regional_variant()
    {
        var handler = new StubHttpHandler((request, _) => JsonResponse(request.RequestUri!.AbsolutePath switch
        {
            "/locales/manifest.json" => "[\"en-US.json\",\"en-GB.json\",\"common.en-GB.json\",\"en-US/checkout.json\"]",
            "/locales/en-US.json" => "{\"Value\":\"US\"}",
            "/locales/en-GB.json" => "{\"Value\":\"GB\"}",
            "/locales/common.en-GB.json" => "{\"Title\":\"Hello from GB\"}",
            "/locales/en-US/checkout.json" => "{\"Buy\":\"Buy now\"}",
            _ => null
        })) { ServeManifest = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client);

        await store.LoadAsync(["en", "en-US"], TestContext.Current.CancellationToken);
        Assert.Equal("GB", store.GetTemplate("Value", new CultureInfo("en")));
        Assert.Equal("Hello from GB", store.GetTemplate("common:Title", new CultureInfo("en")));
        Assert.Equal("Buy now", store.GetTemplate("checkout:Buy", new CultureInfo("en-US")));
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.GetTemplateAsync("Value", new CultureInfo("en-AU"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Without_manifest_a_missing_region_does_not_use_a_sibling_as_fallback()
    {
        var handler = new StubHttpHandler((request, _) => JsonResponse(request.RequestUri!.AbsolutePath.EndsWith("/en-US.json", StringComparison.Ordinal)
            ? "{\"Value\":\"US\"}" : null));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client);
        await Assert.ThrowsAsync<FileNotFoundException>(() => store.GetTemplateAsync("Value", new CultureInfo("en-GB"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Manifest_rejects_paths_outside_resources_path()
    {
        var handler = new StubHttpHandler((_, _) => JsonResponse("[\"../secret.json\"]")) { ServeManifest = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client);
        await Assert.ThrowsAsync<ArgumentException>(() => store.GetTemplateAsync("Value", new CultureInfo("en-US"), TestContext.Current.CancellationToken));
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Refresh_revalidates_etag_and_loads_new_manifest_files()
    {
        var includeNamespace = false;
        var conditional = false;
        var handler = new StubHttpHandler((request, _) =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("/manifest.json", StringComparison.Ordinal))
                return JsonResponse(includeNamespace ? "[\"en-US.json\",\"en-US.common.json\"]" : "[\"en-US.json\"]");
            if (path.EndsWith("/en-US.common.json", StringComparison.Ordinal))
                return JsonResponse("{\"Title\":\"New namespace\"}");
            conditional = request.Headers.IfNoneMatch.Any(tag => tag.Tag == "\"v1\"") && request.Headers.CacheControl?.NoCache == true;
            if (conditional)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotModified));
            var response = CreateJsonResponse("{\"Value\":\"Original\"}");
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\"");
            return Task.FromResult(response);
        }) { ServeManifest = true };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client);
        var culture = new CultureInfo("en-US");

        await store.LoadAsync(culture, TestContext.Current.CancellationToken);
        includeNamespace = true;
        await store.RefreshStoreAsync(TestContext.Current.CancellationToken);
        Assert.True(conditional);
        Assert.Equal("Original", store.GetTemplate("Value", culture));
        Assert.Equal("New namespace", store.GetTemplate("common:Title", culture));
    }

    #if NET8_0_OR_GREATER
    [Fact]
    public async Task Independent_cultures_can_load_while_another_request_is_pending()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new StubHttpHandler(async (request, token) =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/fr-FR.json", StringComparison.Ordinal))
            {
                started.SetResult();
                await release.Task.WaitAsync(token);
            }
            return CreateJsonResponse("{\"Value\":\"ok\"}");
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        using var store = new HttpJsonStore(client);
        var pending = store.GetTemplateAsync("Value", new CultureInfo("fr-FR"), TestContext.Current.CancellationToken);
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal("ok", await store.GetTemplateAsync("Value", new CultureInfo("it-IT"), TestContext.Current.CancellationToken)
                .WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
        }
        finally { release.SetResult(); }
        Assert.Equal("ok", await pending);
    }
    #endif

    [Theory]
    [InlineData("../secret.json")]
    [InlineData("/secret.json")]
    [InlineData("https://elsewhere.test/file.json")]
    [InlineData("folder/../secret.json")]
    [InlineData("%2e%2e/secret.json")]
    public void Rejects_file_mappings_outside_resources_path(string mapping)
    {
        using var client = new HttpClient { BaseAddress = new Uri("https://example.test/") };
        var options = new HttpJsonStoreOptions();
        options.FileMappings["it-IT"] = mapping;
        Assert.Throws<ArgumentException>(() => new HttpJsonStore(client, options));
    }

    [Theory]
    [InlineData("../locales")]
    [InlineData("/locales")]
    [InlineData("%2e%2e/locales")]
    public void Rejects_resources_paths_that_can_escape_the_base_address(string path)
    {
        using var client = new HttpClient { BaseAddress = new Uri("https://example.test/") };
        Assert.Throws<ArgumentException>(() => new HttpJsonStore(client, new HttpJsonStoreOptions { ResourcesPath = path }));
    }

    private static HttpResponseMessage CreateJsonResponse(string? json) => json is null
        ? new HttpResponseMessage(HttpStatusCode.NotFound)
        : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public ConcurrentBag<string> Requests { get; } = [];
        public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } = respond;
        public bool ServeManifest { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsolutePath);
            if (!ServeManifest && request.RequestUri.AbsolutePath.EndsWith("/manifest.json", StringComparison.Ordinal))
                return JsonResponse(null);
            return Respond(request, cancellationToken);
        }
    }

    private static Task<HttpResponseMessage> JsonResponse(string? json) => Task.FromResult(CreateJsonResponse(json));
}
