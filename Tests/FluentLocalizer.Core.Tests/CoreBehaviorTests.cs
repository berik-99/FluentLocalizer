using System.Globalization;
using FluentLocalizer.Logging;

namespace FluentLocalizer.Test;

public sealed class CoreBehaviorTests
{
    [Fact]
    public void Constructors_reject_null_dependencies_and_keys()
    {
        Assert.Throws<ArgumentNullException>(() => new Translator(null!));
        Assert.Throws<ArgumentNullException>(() => new TranslationBuilder(null!, "key"));
        Assert.Throws<ArgumentNullException>(() => new TranslationBuilder(new Store(), null!));
        Assert.Throws<ArgumentNullException>(() => new Translator(new Store()).Get(null!));
    }

    [Fact]
    public async Task Sync_and_async_resolution_handle_missing_null_empty_and_key_not_found_templates()
    {
        var store = new Store();
        store.Values["empty"] = "  ";
        store.Values["key-not-found"] = new KeyNotFoundException();
        var translator = new Translator(store);

        Assert.Equal("[missing]", translator.Get("missing").Resolve());
        Assert.Equal("[empty]", translator.Get("empty").Resolve());
        Assert.Equal("[key-not-found]", translator.Get("key-not-found").Resolve());
        var cancellationToken = TestContext.Current.CancellationToken;
        Assert.Equal("[missing]", await translator.Get("missing").ResolveAsync(cancellationToken));
        Assert.Equal("[empty]", await translator.Get("empty").ResolveAsync(cancellationToken));
        Assert.Equal("[key-not-found]", await translator.Get("key-not-found").ResolveAsync(cancellationToken));
    }

    [Fact]
    public void Uses_requested_culture_for_store_lookup_and_fallback_tokens()
    {
        var store = new Store();
        var builder = new Translator(store, new TranslationOptions
        {
            MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
            MissingKeyFallbackValue = "{KEY}/{Culture}/{0}/{1}"
        }).Get("welcome").WithCulture("fr-FR");

        Assert.Equal("welcome/fr-FR/welcome/fr-FR", builder.Resolve());
        Assert.Equal(new CultureInfo("fr-FR"), store.LastCulture);
        Assert.Equal("fr-FR", store.LastCulture?.Name);
    }

    [Fact]
    public async Task Runtime_arguments_override_defaults_case_insensitively_without_mutating_defaults()
    {
        var defaults = new Dictionary<string, object?> { ["Name"] = "Ada", ["city"] = "Rome" };
        var options = new TranslationOptions { DefaultArguments = defaults };
        var store = new Store();
        store.Values["welcome"] = "Hello {NAME} from {City}";
        var builder = new Translator(store, options).Get("welcome").WithArg("name", "Grace");

        Assert.Equal("Hello Grace from Rome", builder.Resolve());
        Assert.Equal("Ada", defaults["Name"]);
        Assert.Equal(2, defaults.Count);
    }

    [Fact]
    public void Formatting_errors_return_the_configured_fallback()
    {
        var store = new Store();
        store.Values["welcome"] = "Hello {missing}";
        var options = new TranslationOptions
        {
            FormattingErrorBehavior = FormattingErrorBehavior.ReturnPlaceholder,
            FormattingErrorFallbackValue = "format:{key}:{culture}"
        };

        Assert.Equal("format:welcome:it-IT", new Translator(store, options).Get("welcome").WithCulture("it-IT").Resolve());
    }

    [Fact]
    public async Task Async_resolution_propagates_store_errors_and_cancellation()
    {
        var failure = new InvalidOperationException("store failed");
        var store = new Store { AsyncFailure = failure };
        var logger = new RecordingLogger();
        var builder = new Translator(store, logger: logger).Get("key");

        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => builder.ResolveAsync(TestContext.Current.CancellationToken)));
        Assert.Contains(logger.Entries, entry => entry.Level == TranslationLogLevel.Error && ReferenceEquals(entry.Exception, failure));

        store.AsyncFailure = null;
        store.CancellationToken = new CancellationToken(canceled: true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.ResolveAsync(store.CancellationToken));
    }

    [Fact]
    public void Exception_factories_can_return_null_fallback_or_propagate_their_failure()
    {
        var options = new TranslationOptions
        {
            FormattingErrorBehavior = FormattingErrorBehavior.ThrowException,
            FormattingErrorExceptionFactory = (_, _) => null!
        };
        var store = new Store();
        store.Values["welcome"] = "{missing}";
        var result = Assert.Throws<TranslationException>(() => new Translator(store, options).Get("welcome").Resolve());
        Assert.Equal("welcome", result.Key);
        Assert.Contains("Unable to resolve translation key 'welcome'", result.Message);

        var factoryFailure = new InvalidOperationException("factory failed");
        options.MissingKeyBehavior = MissingTranslationBehavior.ThrowException;
        options.MissingKeyExceptionFactory = (_, _) => throw factoryFailure;
        Assert.Same(factoryFailure, Assert.Throws<InvalidOperationException>(() => new Translator(store, options).Get("missing").Resolve()));
    }

    [Fact]
    public void Formatting_exceptions_include_inner_detail_and_options_validate_keys()
    {
        var exception = new TranslationOptions().CreateFormattingException("key", CultureInfo.InvariantCulture, new FormatException("bad format"));
        Assert.IsType<TranslationException>(exception);
        Assert.Contains("bad format", exception.Message);
        Assert.Throws<ArgumentException>(() => new TranslationOptions().CreateMissingKeyException(" ", null));
        Assert.Throws<ArgumentException>(() => new TranslationOptions().CreateFormattingException("", null, null));
    }

    private sealed class Store : ITranslationStore
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        public CultureInfo? LastCulture { get; private set; }
        public Exception? AsyncFailure { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public string? GetTemplate(string key, CultureInfo culture)
        {
            LastCulture = culture;
            if (!Values.TryGetValue(key, out var template)) return null;
            if (template is Exception exception) throw exception;
            return (string?)template;
        }

        public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
        {
            LastCulture = culture;
            if (AsyncFailure is not null) throw AsyncFailure;
            cancellationToken.ThrowIfCancellationRequested();
            if (!Values.TryGetValue(key, out var template)) return Task.FromResult<string?>(null);
            if (template is Exception exception) throw exception;
            return Task.FromResult((string?)template);
        }
    }

    private sealed class RecordingLogger : ITranslationLogger
    {
        public List<(TranslationLogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];
        public void Log(TranslationLogLevel level, string message, Exception? exception = null) => Entries.Add((level, message, exception));
    }
}
