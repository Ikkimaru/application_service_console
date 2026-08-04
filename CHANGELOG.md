# Changelog

All notable user-visible changes are recorded here. This project follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and Semantic Versioning.

## [Unreleased]

### Added

- Added preflighted local command execution for enabled services with Start, Stop, and Restart controls.
- Added live stdout/stderr display, runtime service states, environment-value redaction, and owned process-tree shutdown.
- Added resizable profile and diagnostics sidebars that retain their widths between launches.

### Changed

- The main window now launches maximized on the active display.
- Replaced full-width service rows with a responsive tile grid that shows four services per row at the standard window width.
- Service tiles now expose lifecycle controls according to each service's enablement, discovery, and runtime state.

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
