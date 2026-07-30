#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FluentLocalizer.Core;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents the supported casing transformations that can be applied to a formatted message.
/// </summary>
public enum LetterCase
{
    /// <summary>
    /// Preserves the original casing of the formatted text.
    /// </summary>
    AsIs,
    /// <summary>
    /// Converts the formatted text to uppercase.
    /// </summary>
    Upper,
    /// <summary>
    /// Converts the formatted text to lowercase.
    /// </summary>
    Lower,
    /// <summary>
    /// Converts the formatted text to camel case.
    /// </summary>
    CamelCase,
    /// <summary>
    /// Converts the formatted text to PascalCase.
    /// </summary>
    PascalCase,
    /// <summary>
    /// Converts the formatted text to snake_case.
    /// </summary>
    SnakeCase,
    /// <summary>
    /// Converts the formatted text to kebab-case.
    /// </summary>
    KebabCase
}
