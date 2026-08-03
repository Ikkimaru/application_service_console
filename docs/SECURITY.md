# Security

Report vulnerabilities privately to the repository owner; do not open a public issue containing exploit or credential details.

Project profiles are local-only and must never contain passwords, tokens, private keys, or connection strings. Local settings store only project roots, environment-file references, and enablement choices under `%LOCALAPPDATA%`. Future log ingestion must redact configured secret patterns before persistence or display.

The application runs as the current user and must not request elevation by default. Process execution, installers, remote backends, and update mechanisms require separate threat reviews before release.
