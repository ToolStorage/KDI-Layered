# Changelog

## [1.0.5] - 2026-05-15

### Fixed
- Tarball now actually ships `Kylin.DI.Layered.Analyzer.dll`. Without an `.npmignore`, npm publish was falling back to `.gitignore`, which excluded the freshly-built DLL. Added an explicit `.npmignore` that excludes only AI artifacts, dotnet build temp, and repo metadata.

## [1.0.4] - 2026-05-15

### Added
- Roslyn analyzer `Kylin.DI.Layered.Analyzer.dll` shipped in `Analyzers/`
  - `KDI001` (Error): same-layer `[Inject]` field
  - `KDI002` (Error): upward `[Inject]` field
- Analyzer runs at compile time / in IDE; zero runtime overhead.
- DLL is built by the publish workflow (`dotnet build`) and not committed to the repo.

### Fixed
- Truncated `.meta` files for asmdef and runtime folders that prevented Unity from registering the assembly when consumed via npm.

## [1.0.0] - 2026-05-15

### Added
- Layer marker interfaces: `IViewLayer`, `IViewModelLayer`, `IApplicationServiceLayer`, `IDomainServiceLayer`, `IDataLayer`
- `LayerLevel` enum for ordering
- `LayerValidator.Validate(Type)` — single-type build-time validation
- `LayerValidator.ValidateAssembly(Assembly)` — bulk validation across an assembly
- `LayerViolationException` thrown on same-layer or upward `[Inject]` references
