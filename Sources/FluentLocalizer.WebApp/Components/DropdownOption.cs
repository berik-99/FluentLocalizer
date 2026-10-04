namespace FluentLocalizer.WebApp.Components;

/// <summary>One choice shown by <see cref="Dropdown{TValue}"/>.</summary>
public sealed record DropdownOption<TValue>(TValue Value, string Label);
