# ADR 0004: Local profiles and overrides

**Status:** Accepted — 2026-08-03

Versioned JSON profiles contain discovery, presentation, and execution intent, but project-specific profiles are excluded from source control. Development profiles live under ignored `profiles.local`; installed profiles, project roots, and developer choices live under `%LOCALAPPDATA%`. This prevents project identity, paths, or secrets from leaking into the repository.
