using FluentLocalizer.Core.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace FluentLocalizer.Core;

/// <summary>
/// Configures FluentLocalizer services after they have been registered with the dependency injection container.
/// </summary>
public sealed class FluentLocalizerBuilder(IServiceCollection services)
{
    /// <summary>
    /// Gets the service collection that stores the FluentLocalizer registrations.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Registers a concrete translation store that will be resolved by the translator.
    /// </summary>
    /// <param name="store">The translation store instance to register.</param>
    /// <returns>The current builder instance.</returns>
    public FluentLocalizerBuilder WithStore(ITranslationStore store)
    {
        Services.AddSingleton(store);
        Services.AddSingleton(_ => store);
        return this;
    }

    /// <summary>
    /// Registers a factory that creates a translation store for the service provider.
    /// </summary>
    /// <param name="factory">A factory used to resolve the translation store.</param>
    /// <returns>The current builder instance.</returns>
    public FluentLocalizerBuilder WithStore(Func<IServiceProvider, ITranslationStore> factory)
    {
        Services.AddSingleton(factory);
        return this;
    }

    /// <summary>
    /// Registers the default translation logger implementation.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    public FluentLocalizerBuilder WithLogger()
    {
        Services.AddSingleton<ITranslationLogger, ILoggerAdapter>();
        return this;
    }
}
