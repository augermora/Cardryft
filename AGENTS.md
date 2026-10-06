# Cardryft contributor and agent instructions

## Purpose

Cardryft is a privacy-first, open-source Windows x64 desktop application for
creating and managing custom Apple Wallet card artwork. It uses C# / .NET 10,
WinForms, xUnit, and the MIT license. Preserve LICENSE and existing repository
files. Milestone 1 establishes the solution foundation; do not build the graphical
card editor or integrations in this milestone.

## Architecture

- Use the Cardryft namespace prefix for all application and test code.
- Core owns domain models, validation, and shared contracts when needed. It must
  not reference UI, infrastructure projects, or platform APIs.
- Imaging, Device, Wallet, and Storage may reference Core. Keep the current
  infrastructure libraries independent of each other.
- App owns WinForms and the composition root and may reference Core and the
  infrastructure libraries. Keep UI types out of Core.
- Avoid circular dependencies, speculative service interfaces, and unnecessary
  abstractions. Infrastructure implementations are deferred in Milestone 1.
- Add packages only for a demonstrated need. Production currently needs none;
  tests use the .NET test SDK, xUnit, and its Visual Studio adapter.

## Coding conventions

- Use file-scoped namespaces, four-space indentation, and normal C# naming:
  PascalCase for types/members and camelCase for parameters/local variables.
- Keep nullable reference types and implicit usings enabled. Target x64;
  libraries/tests use net10.0 and App uses net10.0-windows.
- Prefer small, explicit types. Validate domain inputs at their entry point and
  test observable behavior with synthetic, non-sensitive examples.
- Keep the WinForms startup path in App. Future services must be composed there,
  not through dependencies from Core to infrastructure.

## Security restrictions

For Milestone 1:

- Do not access Apple Wallet, connect to an iPhone, or implement private iOS protocols.
- Do not request payment credentials or process card numbers, CVVs, PINs, or bank credentials.
- Do not introduce telemetry or application network communication.
- Do not store pairing records or real device data.
- Do not require administrator privileges; App uses an asInvoker manifest.
- Do not execute downloaded third-party binaries or external device tools.
  Normal NuGet restore and the required .NET/xUnit validation toolchain are allowed.
- Do not modify files outside C:\Dev\Cardryft. Keep tool state, NuGet caches,
  and temporary files inside the ignored .local directory using the validation script.
- Never put secrets, .env files, certificates, pairing records, device data,
  local backups, or diagnostic dumps into tracked files. Use the dedicated
  ignored directories documented in .gitignore, and review untracked files.
- Update docs/security/threat-model.md when inputs, persistence, integrations,
  packages, or other trust boundaries change. Future functionality requires a
  fresh security review; this foundation does not authorize integrations.

## Validation

Run `.\scripts\validate.ps1` from the repository root. It executes:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Report the exact commands executed, warning/error counts, total/passed/failed/
skipped test counts, files changed, dependencies added, architecture decisions,
and unresolved issues. Do not claim success for commands that were not run.
Use meaningful tests for validation behavior. Check project references and the
final diff for dependency violations and accidental sensitive files.

## Git restrictions

Do not initialize another repository, change Git configuration, create commits,
amend/rebase or otherwise modify Git history, push, or publish autonomously.
Leave all changes uncommitted for human review. Preserve the existing repository.
