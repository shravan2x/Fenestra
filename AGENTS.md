# Fenestra

Fenestra is an experimental X11 display server written in C#/.NET 10. See `README.md` for project goals and `docs/` for architecture and roadmap details.

## Cursor Cloud specific instructions

### Prerequisites

- **.NET 10 SDK** (`10.0.201` or later in the 10.0.2xx feature band) is required. The `global.json` at the repo root pins the SDK version with `rollForward: latestFeature`.
- No external databases, Docker, or third-party services are needed.

### Common commands

| Task | Command |
|---|---|
| Restore packages | `dotnet restore` |
| Build | `dotnet build` |
| Run | `dotnet run --project src/Fenestra.App` |
| Lint / format check | `dotnet format --verify-no-changes` |
| Auto-fix formatting | `dotnet format` |
| Test | `dotnet test` (no test projects exist yet) |

### Gotchas

- The apt `dotnet-sdk-10.0` package on Ubuntu 24.04 installs SDK 10.0.1xx (feature band 1), which does **not** satisfy `global.json`'s requirement for 10.0.2xx. Use the official install script (`https://dot.net/v1/dotnet-install.sh --channel 10.0`) targeting `/usr/share/dotnet` instead.
- The project is in Phase 0 bootstrap: the application runs to completion (not a long-running server yet) and prints startup diagnostics. It does not listen on a real socket.
- The `NativeHost.Win32` project compiles on Linux but its runtime behaviour is stubbed since Win32 APIs are unavailable.
