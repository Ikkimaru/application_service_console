# ADR 0006: Port discovery and temporary overrides

**Status:** Accepted — 2026-08-03

Ports are discovered from effective command arguments and Spring Boot configuration rather than compiled into the application. An occupied port is a warning before launch. Users may cancel or approve a session-only free-port override; persistence requires a separate explicit action.
