# Goals

## Product goal

Provide one dependable desktop console for discovering, configuring, running, and diagnosing services across multiple local application projects.

## Success criteria

- New services appear through discovery without application releases or fixed inventories.
- Project-specific profiles, paths, and secrets remain local and excluded from source control.
- Lifecycle actions are deterministic, dependency-aware, reversible, and transparent.
- High-volume logs remain responsive, searchable, exportable, and bounded by retention policy.
- Invalid or ambiguous configuration is explained before destructive or unreliable actions occur.

## Deferred capabilities

- Persisting and searching managed-service logs
- Dependency-aware bulk lifecycle operations
- Graceful protocol-specific shutdown and orphan recovery
- Docker, WSL, or installed Windows-service execution
- Installers, automatic updates, or remote service management
