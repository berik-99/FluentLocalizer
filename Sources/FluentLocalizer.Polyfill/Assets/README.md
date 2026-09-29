# FluentLocalizer.Polyfill

Shared compatibility helpers for FluentLocalizer projects targeting .NET Standard 2.0 and modern .NET.

Use `Guard.IfNull(value)`, `Guard.IfNullOrEmpty(value)`, and `Guard.IfNullOrWhiteSpace(value)` from `FluentLocalizer.Polyfill`. The package also supplies the missing string and HTTP content overloads on .NET Standard 2.0. PolySharp is configured for every source assembly in `Sources/Directory.Build.props`, because its compiler support types must be generated in each assembly that uses them.
