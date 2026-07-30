using System.Runtime.CompilerServices;

namespace FluentLocalizer.Core.Polyfill;

internal static class Throw
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfNull(object value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(value, paramName);
#elif NETSTANDARD2_0
        if (value is null) throw new ArgumentNullException(paramName);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfNullOrEmpty(string value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
#elif NETSTANDARD2_0
        if (string.IsNullOrEmpty(value)) throw new ArgumentException("Value cannot be null or empty.", paramName);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IfNullOrWhiteSpace(string value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
#if NET8_0_OR_GREATER
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
#elif NETSTANDARD2_0
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value cannot be null or empty.", paramName);
#endif
    }
}