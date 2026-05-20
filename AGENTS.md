# AGENTS.md

## Cursor Cloud specific instructions

### Project overview

CoreECS is a pure C# library (no servers, no databases, no Docker). It consists of two projects in `CoreECS.sln`:

| Project | Path | Purpose |
|---------|------|---------|
| `ECS` | `ECS/ECS.csproj` | Library (targets `net8.0` + `netstandard2.1`) |
| `Test` | `Test/Test.csproj` | NUnit tests (targets `net8.0`) |

### Prerequisites

- **.NET 8 SDK** is required. The `global.json` pins `8.0.0` with `rollForward: latestMinor`.
- The update script installs the SDK to `$HOME/.dotnet` and adds it to PATH via `~/.bashrc`.

### Common commands

| Action | Command |
|--------|---------|
| Restore | `dotnet restore` |
| Build | `dotnet build` |
| Test | `dotnet test --verbosity normal` |
| Build Release | `dotnet build --configuration Release` |

### Gotchas

- The .NET SDK is installed to `$HOME/.dotnet`, not `/usr/share/dotnet`. The update script ensures `DOTNET_ROOT` and `PATH` are set in `~/.bashrc`.
- No lint tool (e.g. `dotnet format`) is configured in the solution. Build warnings serve as the primary code-quality check.
- The library uses `AllowUnsafeBlocks` and `LangVersion 9`; the test project uses `ImplicitUsings: enable` and `Nullable: enable`—do not unify these settings.
