# ADR 0008: Local process execution

**Status:** Accepted — 2026-08-04

Single-service execution uses a Windows-local executor behind `IServiceExecutor`. A service can start only when discovery found one unambiguous application entry point, the service is explicitly enabled, and preflight resolves a valid command.

Commands run without a command shell. Arguments are passed individually, working directories and environment files must remain inside the configured project root, and executables must resolve from that working directory or the current process `PATH`. Environment-file values are never included in diagnostics and are redacted from captured output.

The executor prevents duplicate launches, reports runtime state, captures stdout and stderr asynchronously, and terminates the entire child process tree for Stop. Restart is a completed Stop followed by Start. Application shutdown disposes the executor and stops processes it owns.

This milestone intentionally excludes dependency-aware bulk execution, graceful application-specific stop protocols, persistent logs, orphan recovery, health checks, and temporary occupied-port overrides.
