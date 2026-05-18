# Changelog

## [1.1.0] - 2026-05-18

### Added
- `IDomainServiceLayer<TOwnedData>` — generic ownership marker. A DomainService declares the Data type it is allowed to mutate by implementing this constructed interface (`where TOwnedData : IDataLayer`). Multiple ownership is supported by implementing the interface several times with different type arguments.
- `OwnerOnlyAttribute` (`Kylin.DI.Layered.OwnerOnlyAttribute`) — apply to a method or property (or a property's set accessor) on a Data layer type to restrict invocation to owning DomainServices. Fully opt-in.
- Analyzer rule `KDI003` (Error): "Owner-only member called from a non-owner". Flags any call to an `[OwnerOnly]` member from a class that does not declare `IDomainServiceLayer<TData>` ownership of the receiving Data type. Self-calls inside the Data class (and its subclasses) are allowed; property reads remain allowed when only the set accessor is marked.

### Notes
- Existing code is unaffected — `[OwnerOnly]` has zero impact unless explicitly applied. Migrate Data types incrementally where you want to lock down mutation.
- Runtime `LayerValidator` continues to enforce `[Inject]` field direction only. `KDI003` is compile-time (Roslyn analyzer) since it analyzes call sites.

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
