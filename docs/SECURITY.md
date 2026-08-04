# Security

Report vulnerabilities privately to the repository owner; do not open a public issue containing exploit or credential details.

Project profiles are local-only and must never contain passwords, tokens, private keys, or connection strings. Local settings store only project roots, environment-file references, and enablement choices under `%LOCALAPPDATA%`. Future log ingestion must redact configured secret patterns before persistence or display.

The application runs as the current user and must not request elevation by default. Local execution never invokes a command shell implicitly: it passes arguments directly, constrains working directories and environment files to the selected project root, and redacts loaded environment values from captured output. Only processes started by the application are eligible for lifecycle control.

Installers, remote backends, update mechanisms, durable log storage, and additional execution providers require separate threat reviews before release.
