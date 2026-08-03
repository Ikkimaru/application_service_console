# Testing

Run the complete local quality gate:

```powershell
dotnet restore ApplicationServiceConsole.slnx --locked-mode
dotnet format ApplicationServiceConsole.slnx --verify-no-changes --no-restore
dotnet build ApplicationServiceConsole.slnx --no-restore --configuration Release
dotnet test ApplicationServiceConsole.slnx --no-build --configuration Release --collect:"XPlat Code Coverage"
```

Discovery tests must create temporary Maven reactors. They must cover module addition/removal, nested reactors, malformed POMs, libraries, ambiguous entry points, stale metadata, and enablement defaults without referencing an external project or asserting a service count.

Port tests bind ephemeral loopback listeners. Never assume a developer port is free. UI logic should be tested through view models; full UI automation is deferred until lifecycle controls are implemented.
