#pragma warning disable IDE0130 // Namespace does not match folder structure
using FluentLocalizer.Core;
using FluentLocalizer.Core.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registers the FluentLocalizer services and builder helpers into an <see cref="IServiceCollection"/>.
/// </summary>
public static class FluentLocalizerServiceCollectionExtensions
{
    /// <summary>
    /// Adds FluentLocalizer services and configures the translator options.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="configure">An optional callback used to configure translation behavior.</param>
    /// <returns>A builder that can register additional FluentLocalizer services.</returns>
    public static FluentLocalizerBuilder AddFluentLocalizer(this IServiceCollection services, Action<TranslationOptions>? configure = null)
    {
        if (configure != null)
            services.Configure(configure);
        services.AddSingleton<ITranslator>(sp =>
        {
            var store = sp.GetRequiredService<ITranslationStore>();
            var options = sp.GetRequiredService<IOptions<TranslationOptions>>().Value;
            var logger = sp.GetService<ITranslationLogger>();
            return new Translator(store, options, logger);
        });
        return new FluentLocalizerBuilder(services);
    }

    /// <summary>
    /// Adds FluentLocalizer services using a preconfigured translation options instance.
    /// </summary>
    /// <param name="services">The service collection to populate.</param>
    /// <param name="translationOptions">The translation options to use.</param>
    /// <returns>A builder that can register additional FluentLocalizer services.</returns>
    public static FluentLocalizerBuilder AddFluentLocalizer(this IServiceCollection services, TranslationOptions translationOptions)
        => AddFluentLocalizer(services, options => options = translationOptions);
}
#pragma warning restore IDE0130 // Namespace does not match folder structure