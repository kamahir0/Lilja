# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]
### Changed
- Switched CRUD semantics to strict staged-view `Create` / `Update` / `Delete` behavior instead of implicit upsert-like writes.
- Changed persisted storage identifiers to use namespace-qualified entity names when needed, so same-named entities no longer collide on disk.
- Narrowed the supported usage model to generated repositories and moved low-level transaction helpers out of the public contract.
- Limited duplicate-instance diagnostics to editor / development-oriented paths instead of treating them as always-on runtime surface.

### Added
- Added runtime surface regression coverage so public repository-facing types stay stable and low-level helpers do not leak back out.

## [0.2.0] - 2026-04-19
### Changed
- Added `InitializeAsync(CancellationToken)` to generated repositories and made JSON / MessagePack initialization explicit.
- Changed generated `Read` APIs to return nullable entities.
- Reworked transactions to use staged RW state so RO transactions only observe committed data.
- Removed public `OnCommit` / `OnRollback` hooks from `IReadWriteTx`.
- Added support for annotated instance fields and auto-properties.
- Made MessagePack optional for both code generation and editor tooling.

### Added
- Added Roslyn diagnostics for invalid entity shapes, invalid value objects, and persisted key mismatches.
- Added `GetKeyFromDto` generation for persisted keyed entities.
- Added xUnit + `CSharpGeneratorDriver` tests for generator and runtime behavior.

### Fixed
- Fixed namespace qualification issues in generated DTO / formatter / repository code.
- Fixed repository viewer to avoid compile-time MessagePack dependency and use generated DTO key helpers when available.

## [0.1.0] - 2026-01-01
### Added
- Initial release.
