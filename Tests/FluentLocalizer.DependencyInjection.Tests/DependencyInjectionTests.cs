using System.Globalization;
using FluentLocalizer;
using FluentLocalizer.Core;
using FluentLocalizer.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FluentLocalizer.Test;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void Configure_overload_registers_singleton_translator_and_applies_options()
    {
        var services = new ServiceCollection();
        services.AddFluentLocalizer(options =>
        {
            options.MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue;
            options.MissingKeyFallbackValue = "missing:{key}";
        }).WithStore(CreateStore());

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ITranslator>();
        var second = provider.GetRequiredService<ITranslator>();

        Assert.Same(first, second);
        Assert.Equal("missing:unknown", first.Get("unknown").Resolve());
    }

    [Fact]
    public void Options_instance_overload_uses_the_supplied_instance()
    {
        var options = new TranslationOptions
        {
            MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
            MissingKeyFallbackValue = "fallback:{key}"
        };
        var services = new ServiceCollection();
        services.AddFluentLocalizer(options).WithStore(CreateStore());

        using var provider = services.BuildServiceProvider();
        Assert.Equal("fallback:missing", provider.GetRequiredService<ITranslator>().Get("missing").Resolve());
        Assert.Same(options, provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<TranslationOptions>>().Value);
    }

    [Fact]
    public void Store_instance_registration_uses_the_same_instance()
    {
        var services = new ServiceCollection();
        var store = CreateStore();
        var builder = services.AddFluentLocalizer().WithStore(store);

        Assert.Same(services, builder.Services);
        using var provider = services.BuildServiceProvider();
        Assert.Same(store, provider.GetRequiredService<ITranslationStore>());
        Assert.Equal("Hello Ada", provider.GetRequiredService<ITranslator>().Get("welcome").WithArg("name", "Ada").Resolve());
    }

    [Fact]
    public void Store_factory_is_created_once_for_the_container()
    {
        var calls = 0;
        var services = new ServiceCollection();
        services.AddFluentLocalizer().WithStore(_ =>
        {
            calls++;
            return CreateStore();
        });

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<ITranslationStore>();
        Assert.Same(first, provider.GetRequiredService<ITranslationStore>());
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Logger_registration_adapts_framework_logs_and_is_optional()
    {
        var providerSink = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(providerSink));
        services.AddFluentLocalizer().WithStore(CreateStore()).WithLogger();
        using var provider = services.BuildServiceProvider();

        var translator = provider.GetRequiredService<ITranslator>();
        Assert.Equal("Hello world", translator.Get("welcome").WithArg("name", "world").Resolve());
        Assert.Equal("[missing]", translator.Get("missing").Resolve());

        Assert.Contains(providerSink.Entries, entry => entry.Level == LogLevel.Debug);
        Assert.Contains(providerSink.Entries, entry => entry.Level == LogLevel.Warning);

        var noLoggerServices = new ServiceCollection();
        noLoggerServices.AddFluentLocalizer().WithStore(CreateStore());
        using var noLoggerProvider = noLoggerServices.BuildServiceProvider();
        Assert.Equal("Hello world", noLoggerProvider.GetRequiredService<ITranslator>().Get("welcome").WithArg("name", "world").Resolve());
    }

    [Fact]
    public void Null_arguments_are_rejected()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() => FluentLocalizerServiceCollectionExtensions.AddFluentLocalizer(
            (IServiceCollection)null!, (Action<TranslationOptions>?)null));
        Assert.Throws<ArgumentNullException>(() => FluentLocalizerServiceCollectionExtensions.AddFluentLocalizer(
            services, (TranslationOptions)null!));

        var builder = new FluentLocalizerBuilder(services);
        Assert.Throws<ArgumentNullException>(() => builder.WithStore((ITranslationStore)null!));
        Assert.Throws<ArgumentNullException>(() => builder.WithStore((Func<IServiceProvider, ITranslationStore>)null!));
    }

    private static ITranslationStore CreateStore() => new TestStore();

    private sealed class TestStore : ITranslationStore
    {
        public string? GetTemplate(string key, CultureInfo culture) => key == "welcome" ? "Hello {name}" : null;
        public Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default) => Task.FromResult(GetTemplate(key, culture));
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Entries);
        public void Dispose() { }

        private sealed class RecordingLogger(List<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
