# Cardryft
Privacy-first, open-source Windows application for creating and managing custom Apple Wallet card artwork.

## Milestone 1: solution foundation

This milestone supplies a modular .NET 10 solution, a minimal WinForms startup
window, a validated artwork-project name, and xUnit tests. The graphical card
editor, image processing, persistence, backups, device communication, and Wallet
integration are not implemented. The application has no network communication
or telemetry and requires no administrator privileges.

## Requirements and validation

- Windows x64 with a .NET 10 SDK and Windows desktop targeting support.
- NuGet connectivity for the initial test-package restore.

From the repository root, run:

```powershell
.\scripts\validate.ps1
```

The script keeps .NET CLI state, NuGet caches, and temporary files in the ignored
`.local/` directory and disables CLI telemetry. Restore uses the repository's
`NuGet.Config` with nuget.org as the only package source. The script executes these commands in order
and stops on failure:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Open `Cardryft.sln` in Visual Studio with .NET 10 support, or launch the startup
window after validation with:

```powershell
dotnet run --project src/Cardryft.App -c Release --no-restore
```

## Projects

| Project | Responsibility |
| --- | --- |
| `src/Cardryft.App` | WinForms UI and application composition root |
| `src/Cardryft.Core` | UI-independent domain models, validation, and future shared contracts |
| `src/Cardryft.Imaging` | Future image processing and artwork generation |
| `src/Cardryft.Device` | Future device abstractions; no communication |
| `src/Cardryft.Wallet` | Future Wallet customization abstractions; no Wallet access |
| `src/Cardryft.Storage` | Future project persistence and backup abstractions |
| `tests/Cardryft.Tests` | Automated domain tests |

The infrastructure libraries currently contain only project definitions. Service
interfaces will be introduced when a concrete feature needs them.

See [architecture](docs/architecture/overview.md), the
[threat model](docs/security/threat-model.md), and [contributor instructions](AGENTS.md).

## Security and license

Cardryft handles artwork, not payment credentials. Do not supply card numbers,
CVVs, PINs, or bank credentials. Milestone 1 does not access Apple Wallet or
iPhones, implement private iOS protocols, or store pairing records. Local
secrets, certificates, device data, backups, and diagnostic dumps are excluded
from Git; ignore rules are not encryption or a substitute for careful review.

Licensed under the [MIT License](LICENSE).
