using Microsoft.Extensions.DependencyInjection;

namespace FluentLocalizer.WebApp;

/// <summary>A component-owned DI container for editable templates and strict error policies.</summary>
internal sealed class PlaygroundSession : IDisposable
{
    private readonly ServiceProvider _services;

    public PlaygroundSession()
    {
        // AddFluentLocalizer registers one singleton translator per container. A separate
        // container keeps each playground's mutable store and options out of the site's catalog.
        var services = new ServiceCollection();
        Store = new PlaygroundStore();
        services.AddFluentLocalizer(options =>
        {
            options.FormattingErrorBehavior = FormattingErrorBehavior.ThrowException;
            options.MissingKeyBehavior = MissingTranslationBehavior.ThrowException;
        }).WithStore(Store);
        _services = services.BuildServiceProvider();
        Translator = _services.GetRequiredService<ITranslator>();
    }

    public PlaygroundStore Store { get; }
    public ITranslator Translator { get; }

    public void Dispose() => _services.Dispose();
}
