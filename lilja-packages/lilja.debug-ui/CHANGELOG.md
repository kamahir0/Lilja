# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]
### Added
- Added builder extension helpers that return created controls for labels, buttons, fields, foldouts, navigation buttons, and virtual foldouts.
- Added click-handler overloads for buttons and a `TempNavigationButton` helper for temporary debug pages.
- Changed `IDebugUIBuilder.VisualElement` to return the added element.

### Fixed
- Fixed `TempNavigationButton` so temporary pages open in the current host, including the editor window, without registering them in the editor page list.

## [0.1.0] - 2026-01-01
### Added
- Initial release.
