#if NETSTANDARD2_0
using FluentLocalizer.Polyfill;
using System.Text;

namespace System;

/// <summary>Provides string operations missing from .NET Standard 2.0.</summary>
internal static class StringExtensions
{
    /// <summary>Tests whether the string contains a character.</summary>
    public static bool Contains(this string str, char value)
    {
        Guard.IfNull(str);
        return str.IndexOf(value) >= 0;
    }

    /// <summary>Tests whether the string starts with a character.</summary>
    public static bool StartsWith(this string str, char value)
    {
        Guard.IfNull(str);
        return str.Length > 0 && str[0] == value;
    }

    /// <summary>Replaces matching text using the requested comparison mode.</summary>
    public static string Replace(this string str, string oldValue, string? newValue, StringComparison comparisonType)
    {
        Guard.IfNull(str, nameof(str));
        Guard.IfNull(oldValue, nameof(oldValue));

        if (oldValue.Length == 0) throw new ArgumentException("String cannot be of zero length.", nameof(oldValue));
        var start = 0;
        var result = new StringBuilder(str.Length);
        while (true)
        {
            var index = str.IndexOf(oldValue, start, comparisonType);
            if (index < 0) break;
            result.Append(str, start, index - start).Append(newValue);
            start = index + oldValue.Length;
        }
        return result.Append(str, start, str.Length - start).ToString();
    }
}
#endif
