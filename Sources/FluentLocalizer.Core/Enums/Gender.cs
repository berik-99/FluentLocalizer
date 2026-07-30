#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace FluentLocalizer.Core;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Represents the supported gender values for message formatting.
/// </summary>
public enum Gender
{
    /// <summary>
    /// Indicates that no explicit gender has been provided.
    /// </summary>
    Unspecified,
    /// <summary>
    /// Indicates a male gender.
    /// </summary>
    Male,
    /// <summary>
    /// Indicates a female gender.
    /// </summary>
    Female,
    /// <summary>
    /// Indicates a gender that is neither male nor female.
    /// </summary>
    Neuter
}
