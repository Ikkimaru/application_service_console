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
