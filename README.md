# Application Service Console

Application Service Console is a Windows desktop workspace for discovering, organizing, and eventually running services across local Java projects. The current `0.1.0-alpha.1` release is the professional architecture scaffold: it discovers Spring Boot services from Maven reactors, enriches them with matching VS Code Java launch metadata, validates project configuration, detects port conflicts, and presents the results in a WPF console.

Process execution and indexed log history are intentionally visible but unavailable until their dedicated milestones. The application never assumes that a project has a fixed number of services.

## Requirements

- Windows 11 x64
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Java and Maven versions compatible with the project being managed

## Build and run

```powershell
dotnet restore ApplicationServiceConsole.slnx --locked-mode
dotnet build ApplicationServiceConsole.slnx --no-restore --configuration Release
dotnet test ApplicationServiceConsole.slnx --no-build --configuration Release
dotnet run --project src/ApplicationServiceConsole.Desktop
```

Portable project profiles are intentionally not committed to this repository. During development, place them in the ignored `profiles.local` directory. Installed applications can load profiles from `%LOCALAPPDATA%\ApplicationServiceConsole\Profiles`. Project paths and service enablement choices are also stored locally.

## How discovery works

Profiles declare discovery rules rather than service names. The Maven provider traverses declared reactor modules and recognizes modules containing a Spring Boot application entry point. Matching `.vscode/launch.json` entries may provide display names, JVM arguments, enablement, and ordering, but stale launch entries cannot create services. Newly discovered services are shown disabled until explicitly enabled.

Profiles are versioned JSON documents validated against `src/ApplicationServiceConsole.Desktop/profiles/schema/profile.schema.json`. See [architecture](docs/ARCHITECTURE.md), [goals](docs/GOALS.md), and [roadmap](ROADMAP.md).

## Project status

This repository is pre-release software. No public license is granted; all rights are reserved. Do not redistribute the source or binaries without explicit permission.
