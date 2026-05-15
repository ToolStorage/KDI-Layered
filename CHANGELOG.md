# Changelog

## [1.0.0] - 2026-05-15

### Added
- Layer marker interfaces: `IViewLayer`, `IViewModelLayer`, `IApplicationServiceLayer`, `IDomainServiceLayer`, `IDataLayer`
- `LayerLevel` enum for ordering
- `LayerValidator.Validate(Type)` — single-type build-time validation
- `LayerValidator.ValidateAssembly(Assembly)` — bulk validation across an assembly
- `LayerViolationException` thrown on same-layer or upward `[Inject]` references
