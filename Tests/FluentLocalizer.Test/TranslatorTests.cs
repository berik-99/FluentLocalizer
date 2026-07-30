using System.Collections.Concurrent;
using System.Globalization;
using FluentLocalizer.Core;
using FluentLocalizer.Core.Logging;

namespace FluentLocalizer.Test;

public class TranslatorTests
{
    [Fact]
    public async Task Resolve_processes_concurrent_translation_requests_without_errors()
    {
        var store = new ConcurrentTestTranslationStore(100);
        var translator = new Translator(store);

        var requests = Enumerable.Range(0, 100)
            .Select(index => Task.Run(() =>
            {
                return translator
                    .Get($"welcome-{index}")
                    .WithArg("name", $"user-{index}")
                    .Resolve();
            }))
            .ToArray();

        var results = await Task.WhenAll(requests);

        Assert.Equal(100, results.Length);
        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal($"Hello user-{index}", results[index]);
        }
    }

    [Fact]
    public async Task ResolveAsync_processes_concurrent_translation_requests_without_errors()
    {
        var store = new ConcurrentTestTranslationStore(100);
        var translator = new Translator(store);

        var requests = Enumerable.Range(0, 100)
            .Select(index => translator
                .Get($"welcome-{index}")
                .WithArg("name", $"user-{index}")
                .ResolveAsync())
            .ToArray();

        var results = await Task.WhenAll(requests);

        Assert.Equal(100, results.Length);
        for (var index = 0; index < results.Length; index++)
        {
            Assert.Equal($"Hello user-{index}", results[index]);
        }
    }

    [Fact]
    public void Get_returns_builder_using_shared_translation_options()
    {
        TestTranslationStore store = new();
        Translator translator = new(
            store,
            new TranslationOptions
            {
                MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
                MissingKeyFallbackValue = "fallback for {key}"
            });

        var builder = translator.Get("missing");

        Assert.Equal("fallback for missing", builder.Resolve());
    }

    [Fact]
    public void Resolve_logs_warning_and_debug_when_missing_key_returns_fallback()
    {
        TestTranslationStore store = new();
        var logger = new TestTranslationLogger();
        Translator translator = new(
            store,
            new TranslationOptions
            {
                MissingKeyBehavior = MissingTranslationBehavior.ReturnConfiguredValue,
                MissingKeyFallbackValue = "fallback for {key}"
            },
            logger);

        var result = translator.Get("missing").Resolve();

        Assert.Equal("fallback for missing", result);
        Assert.Contains(logger.Entries, entry => entry.Level == TranslationLogLevel.Debug);
        Assert.Contains(logger.Entries, entry => entry.Level == TranslationLogLevel.Warning);
    }

    [Fact]
    public void Resolve_logs_error_when_missing_key_throws_exception()
    {
        TestTranslationStore store = new();
        var logger = new TestTranslationLogger();
        Translator translator = new(
            store,
            new TranslationOptions
            {
                MissingKeyBehavior = MissingTranslationBehavior.ThrowException
            },
            logger);

        var exception = Assert.Throws<TranslationException>(() => translator.Get("missing").Resolve());

        Assert.NotNull(exception);
        Assert.Contains(logger.Entries, entry => entry.Level == TranslationLogLevel.Error);
    }

    [Fact]
    public void Resolve_logs_debug_for_successful_translation()
    {
        TestTranslationStore store = new();
        store.AddTemplate("welcome", "Hello {name}");
        var logger = new TestTranslationLogger();
        Translator translator = new(
            store,
            new TranslationOptions(),
            logger);

        var result = translator.Get("welcome").WithArg("name", "world").Resolve();

        Assert.Equal("Hello world", result);
        Assert.Contains(logger.Entries, entry => entry.Level == TranslationLogLevel.Debug);
    }

    private sealed class ConcurrentTestTranslationStore : ITranslationStore
    {
        private readonly ConcurrentDictionary<string, string?> _templates = new(StringComparer.OrdinalIgnoreCase);

        public ConcurrentTestTranslationStore(int templateCount)
        {
            for (var index = 0; index < templateCount; index++)
            {
                _templates[$"welcome-{index}"] = "Hello {name}";
            }
        }

        public string? GetTemplate(string key, CultureInfo culture)
        {
            Thread.Sleep(10);
            return _templates.TryGetValue(key, out var template) ? template : null;
        }

        public async Task<string?> GetTemplateAsync(string key, CultureInfo culture, CancellationToken cancellationToken = default)
        {
            await Task.Delay(10, cancellationToken);
            return _templates.TryGetValue(key, out var template) ? template : null;
        }
    }

    private sealed class TestTranslationLogger : ITranslationLogger
    {
        public List<(TranslationLogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public void Log(TranslationLogLevel level, string message, Exception? exception = null)
            => Entries.Add((level, message, exception));
    }
}
