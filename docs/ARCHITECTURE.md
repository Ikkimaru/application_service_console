# Architecture

The solution uses four layers:

- **Domain** owns profile, discovery, service, diagnostic, port, execution, and log models.
- **Application** owns use-case contracts, profile validation, discovery coordination, enablement resolution, and dependency-graph rules.
- **Infrastructure** owns JSON files, Maven/Spring scanning, VS Code metadata, YAML port resolution, filesystem monitoring, and dependency registrations.
- **Desktop** owns WPF composition, presentation state, folder selection, resources, and the generic profile schema.

Dependencies point inward. Infrastructure implementations are selected by provider `type`, allowing later executors and discovery providers without changing domain models.

## Discovery flow

1. Load a profile from the ignored repository-local area or the user's application-data directory.
2. Traverse the Maven reactor from its root POM.
3. Identify modules containing Spring Boot application entry points.
4. Assign stable Maven-coordinate identities.
5. Enrich only those services with matching editor metadata.
6. Apply portable and local overrides.
7. Resolve ports and validate identities, dependencies, entry points, and conflicts.
8. Present an immutable discovery snapshot.

Filesystem watching is debounced. Cached snapshots improve responsiveness but never replace a fresh filesystem scan as the authority.

## Local execution flow

1. Require a discovered, unambiguous, explicitly enabled service with a resolved command.
2. Resolve the executable, arguments, working directory, and optional environment file without invoking a shell.
3. Reject paths outside the selected project root and commands whose executable cannot be found.
4. Start one owned process tree and publish runtime-state changes.
5. Drain stdout and stderr asynchronously into a bounded live UI view, redacting environment-file values.
6. Stop the entire owned process tree on request or application shutdown.

Bulk ordering, graceful protocol-specific shutdown, health checks, and durable indexed logs remain separate application milestones.

## Desktop layout persistence

Resizable presentation settings are machine-local application preferences, separate from portable project profiles and per-profile project settings. Sidebar widths are stored under `%LOCALAPPDATA%\ApplicationServiceConsole`, validated against UI constraints when loaded, and never affect discovery or execution behavior.
