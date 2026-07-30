#pragma warning disable IDE0130 // Namespace does not match folder structure
using FluentLocalizer.Core.Polyfill;
using System.Text.RegularExpressions;

namespace System;

internal static class StringExtensions
{
    public static string Replace(this string str, string oldValue, string newValue, StringComparison comparisonType)
    {
        Throw.IfNull(str, nameof(str));
        Throw.IfNull(oldValue, nameof(oldValue));

        if (comparisonType is StringComparison.Ordinal or StringComparison.CurrentCulture or StringComparison.InvariantCulture)
        {
            return str.Replace(oldValue, newValue);
        }

        var options = RegexOptions.None;

        if (comparisonType is StringComparison.OrdinalIgnoreCase or StringComparison.InvariantCultureIgnoreCase)
        {
            options = RegexOptions.IgnoreCase;
        }
        else if (comparisonType is StringComparison.CurrentCultureIgnoreCase)
        {
            options = RegexOptions.IgnoreCase;
        }

        return Regex.Replace(str, Regex.Escape(oldValue), newValue ?? string.Empty, options);
    }
}

#pragma warning restore IDE0130 // Namespace does not match folder structure