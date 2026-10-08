# Cardryft contributor and agent instructions

## Purpose

Cardryft is a privacy-first, open-source Windows x64 desktop application for
creating and managing custom Apple Wallet card artwork. It uses C# / .NET 10,
WinForms, xUnit, and the MIT license. Preserve LICENSE and existing repository
files. Milestone 3 adds offline project persistence, history, and async preview.
Milestone 4F adds a staged C candidate, hardened loader and offline ABI fixtures
alongside inactive preflight and a fake-testable read-only USB boundary.
Real communication is blocked by host-authority/resource gates;
Apple Wallet integration remains prohibited.
Milestone 4G reviewed these gates and stopped without native changes or promotion.
Windows identity checks and pairing-record provenance remain unimplemented; the
default encrypted-key password callback and complete resource/cleanup ceilings
remain blockers. A successful offline fixture or hash audit must not override them.

## Architecture

- Use the Cardryft namespace prefix for all application and test code.
- Core owns domain models, validation, and shared contracts when needed. It must
  not reference UI, infrastructure projects, or platform APIs.
- Imaging, Device, Wallet, and Storage may reference Core. Keep the current
  infrastructure libraries independent of each other.
- App owns WinForms and the composition root and may reference Core and the
  infrastructure libraries. Keep UI types out of Core.
- Avoid circular dependencies, speculative service interfaces, and unnecessary
  abstractions. Imaging owns raster loading/rendering/export; keep image processing
  out of WinForms event handlers. Storage owns versioned project/recent JSON.
  Centralize per-user LocalApplicationData paths and project-relative source
  normalization/resolution in Storage. Keep absolute version 1 references readable.
  Device owns its internal native preflight/backend; Wallet remains deferred. Keep history value-only and bounded; serialize
  preview work, suppress stale results, and await workers before disposing image data.
- Keep artwork rasters rectangular; rounded masking/decorations belong only to
  App's preview. Share transform/crop logic through Imaging. ArtworkSize.Canonical
  is a provisional Cardryft editor/export size, not an Apple Wallet requirement.
- Add packages only for a demonstrated need. Imaging uses System.Drawing.Common;
  tests use the .NET test SDK, xUnit v3 (mtp-off), and its Visual Studio adapter.

## Coding conventions

- Use file-scoped namespaces, four-space indentation, and normal C# naming:
  PascalCase for types/members and camelCase for parameters/local variables.
- Keep nullable reference types and implicit usings enabled. Target x64;
  Core and inactive libraries use net10.0; App, Imaging, and raster tests use
  net10.0-windows. Core must stay platform independent.
- Prefer small, explicit types. Validate domain inputs at their entry point and
  test observable behavior with synthetic, non-sensitive examples.
- Keep the WinForms startup path in App. Future services must be composed there,
  not through dependencies from Core to infrastructure.

## Security restrictions

For the current device boundary and Milestone 4F candidate:

- Do not access Apple Wallet. Normal discovery must not load DLLs or call devices;
  source-owned offline ABI probes are permitted only under the exception below.
  If no safe exact x64 native closure exists, stop at tested abstractions and report
  the blocker. Never adopt an opaque tool distribution or redistribute Apple binaries.
  A later reviewed backend may query only DeviceName, ProductType, ProductVersion
  and BuildVersion over USB with existing confirmed trust. No Pair/Unpair, record
  changes, generic dictionaries, StartService, file/app/content access or device writes.
  After automated validation, provide explicit instructions and wait for the owner
  to physically connect a phone before any hardware use. Never automate acceptance
  of Trust This Computer or reset existing trust. Keep identifiers transient/internal.
- Do not request payment credentials or process card numbers, CVVs, PINs, or bank credentials.
- Do not introduce telemetry or application network communication.
- Keep image imports bounded, reject unsupported/corrupt files, release source
  handles after import, and strip metadata on export. Use synthetic images in tests.
- Treat project JSON as untrusted and bounded. Store only editor values and local
  image references; preserve project state on missing sources. Keep recent paths
  local-only and prompts explicit before discarding unsaved work.
- Do not store pairing records or real device data.
- Do not require administrator privileges; App uses an asInvoker manifest.
- Do not execute downloaded third-party binaries or external device tools.
  Normal NuGet restore and the required .NET/xUnit validation toolchain are allowed.
  Milestone 4D additionally authorizes the exact hash-locked repository-local native
  compiler/build/inspection toolchain in native-build, with caches/state under .local.
  Milestone 4F additionally permits source-owned offline parser/TLS fixtures and
  validated ABI version/initialize/release probes. These must never invoke
  USB enumeration, usbmux/lockdown calls, pairing, or hardware use. Build artifacts
  remain staged until reproducibility, dependency, licensing and interop gates pass.
- Milestone 4E's [runtime safety audit](docs/research/native-runtime-safety.md)
  rejects the exact 4D candidate. Do not enable interop, load/promote its DLLs, or
  use hardware. Reproducible hashes do not prove authenticated existing trust,
  four-key-only native reads, immutable records, or bounded allocations/cleanup.
  Native hardening changes require newly reviewed pins and two clean builds;
  never substitute modified binaries under the old hashes. A managed timeout
  cannot abort C work; SafeHandle finalizers must not perform unbounded protocol I/O.
  Keep transport endpoints immutable and reject redirects without global
  environment mutation. Native limits must precede allocation/parsing/marshalling.
- Milestone 4F's narrow C boundary uses only the owner-approved fixed Windows IPC
  endpoint 127.0.0.1:27015. No alternate address or environment override is allowed;
  network-device entries must never be connected. The default application backend
  stays inactive until all promotion gates pass and hardware use is authorized.
  A source-owned offline probe may load a hash-validated candidate and initialize/
  free its context, but may not enumerate/open/query devices. Never reuse 4D hashes.
- Milestone 4G's [stopped promotion review](docs/research/native-runtime-safety.md)
  is not authorization to contact even the approved endpoint. Socket owner PID,
  executable/signature/service evidence and existing protected-record provenance
  must all be independently reviewed before any protocol output. Missing or denied
  evidence fails closed without elevation. Do not parse encrypted record keys using
  the staged default password callback. Reverify unchanged 4F artifacts with
  `native-build/reverify-4f.ps1 -Label <new-label>`; never overwrite prior evidence.
  Native changes require two fresh clean builds, new pins and replacement validation.
- Application recent-project state belongs in Windows LocalApplicationData under
  Cardryft, never beside the executable. Tests must inject repository-local paths;
  unavailable application data must remain nonfatal. Projects reference images
  without copying/embedding them; document absolute fallback path disclosure.
- Development actions must not modify files outside C:\Dev\Cardryft. Keep tool state, NuGet caches,
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
