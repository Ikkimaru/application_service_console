# Changelog

All notable user-visible changes are recorded here. This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and Semantic Versioning.

## [Unreleased]

### Changed

- Replaced full-width service rows with a responsive tile grid that shows four services per row at the standard window width.
- Disabled process controls now explain that execution is not implemented yet when hovered.

### Fixed

- Enforced the dark palette across the main workspace, lists, and disabled buttons so text and controls remain readable.
- Restored first-use project-folder selection with a generic Maven profile and launch-directory-independent local profile discovery.

## [0.1.0-alpha.1] - 2026-08-03

### Added

- Clean .NET 10 WPF solution with strict architecture boundaries and centralized dependency management.
- Dynamic Maven reactor and Spring Boot service discovery without a fixed project inventory.
- Optional VS Code Java launch metadata enrichment that ignores stale entries.
- Versioned local profiles with machine-local project roots and enablement choices excluded from source control.
- Spring Boot port discovery, configuration precedence, and occupied-port diagnostics.
- Dark desktop scaffold showing profiles, services, enablement, diagnostics, execution order metadata, and future log controls.
- Automated tests, locked restores, GitHub Actions, Dependabot, governance documentation, and agent instructions.
