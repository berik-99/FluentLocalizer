namespace FluentLocalizer.Polyfill;

/// <summary>Provides cancellation-aware HTTP content access on older targets.</summary>
internal static class HttpContentExtensions
{
    /// <summary>Opens a content stream while honoring cancellation where supported.</summary>
    public static Task<Stream> ReadAsStreamAsync(this HttpContent content, CancellationToken cancellationToken)
    {
        Guard.IfNull(content);
#if NET8_0_OR_GREATER
        return content.ReadAsStreamAsync(cancellationToken);
#else
        cancellationToken.ThrowIfCancellationRequested();
        return content.ReadAsStreamAsync();
#endif
    }
}
