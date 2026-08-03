# Agent instructions

These rules apply to the entire repository.

## Required workflow

- Read `docs/GOALS.md`, `docs/ARCHITECTURE.md`, `TASKS.md`, and relevant ADRs before changing behavior.
- Keep Domain independent; Application may depend only on Domain; Infrastructure may depend on Application and Domain; Desktop is the composition/UI layer.
- Do not encode or test a fixed service count or fixed project inventory. Discovery rules and synthetic repositories are the source of truth.
- Do not create services from editor metadata alone. A discovery provider must identify a runnable service first.
- Newly discovered services must require explicit enablement unless a future ADR changes the policy.
- Keep project-specific profiles under ignored `profiles.local` or the user's local application-data directory. Never commit project names, project-specific launch metadata, secrets, environment values, absolute developer paths, or raw credentials.
- Update `CHANGELOG.md` for user-visible changes, `TASKS.md` for milestone status, and an ADR for consequential reasoning. Use Git history for implementation detail rather than creating duplicate logs.
- Use `apply_patch` for hand-edited files. Preserve unrelated user changes.

## Git restrictions

Agents must never modify Git state, repository history, remotes, or pull requests. The user performs all Git and GitHub changes manually.

Allowed Git commands are limited to read-only inspection:

- `git status`
- `git log`
- `git branch --list`
- `git ls-files`

Agents must not stage, commit, amend, reset, restore, rebase, merge, cherry-pick, switch branches, create branches or tags, fetch, pull, push, modify remotes, or create, update, merge, or close pull requests.

## Quality gate

Run all of the following before completion:

```powershell
dotnet restore ApplicationServiceConsole.slnx --locked-mode
dotnet format ApplicationServiceConsole.slnx --verify-no-changes --no-restore
dotnet build ApplicationServiceConsole.slnx --no-restore --configuration Release
dotnet test ApplicationServiceConsole.slnx --no-build --configuration Release
```

Warnings are errors. Do not weaken analyzers or tests merely to pass a build. Tests involving Java projects must create synthetic temporary Maven reactors and must not depend on an external project checkout.
