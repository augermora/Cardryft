# Architecture overview

Cardryft is a modular .NET 10 Windows x64 application. Milestone 1 establishes
project boundaries and a minimal WinForms startup window without an editor.
The executable is framework-dependent and uses the installed .NET 10 Windows
Desktop runtime. An explicit asInvoker manifest avoids elevation requests.

## Dependency direction

```text
Cardryft.App ----> Cardryft.Core
             |--> Cardryft.Imaging ----> Cardryft.Core
             |--> Cardryft.Device  ----> Cardryft.Core
             |--> Cardryft.Wallet  ----> Cardryft.Core
             `--> Cardryft.Storage ----> Cardryft.Core

Cardryft.Tests --> Cardryft.Core
```

Core has no project or package dependencies and no WinForms or platform types.
Infrastructure libraries depend only on Core. There are no reverse references,
infrastructure-to-infrastructure references, or circular dependencies. Every
namespace starts with Cardryft. Shared build properties enable nullable reference
types, implicit usings, and x64 compilation. The SDK selector accepts stable
.NET 10 feature bands.

## Responsibilities and present behavior

- **App:** Program is the composition root. It initializes WinForms and opens
  MainForm, which displays only the application name and foundation status.
- **Core:** ArtworkProject holds an immutable display name, trims surrounding
  whitespace, and rejects null or blank names. It has no artwork bytes, payment
  fields, device identifiers, persistence, or I/O.
- **Imaging:** Reserved for image processing and artwork generation.
- **Device:** Reserved for device abstractions. No discovery, pairing, or transport.
- **Wallet:** Reserved for customization abstractions. No Apple Wallet access.
- **Storage:** Reserved for project persistence and backup abstractions. No file I/O.
- **Tests:** xUnit tests exercise accepted names, normalization, and invalid inputs.

The four infrastructure libraries intentionally have no service classes or
interfaces yet. Define contracts only when a feature establishes actual needs;
shared domain-facing contracts belong in Core and implementations in the owning
module. App will wire implementations when they exist.

## Dependencies and deferred decisions

Production uses only .NET framework references. Tests require
Microsoft.NET.Test.Sdk 17.14.1, xunit.v3.mtp-off 4.0.1, and
xunit.runner.visualstudio 4.0.0; the adapter is a private test dependency.
The xUnit v3 test project is an executable, with its entry point supplied by
xUnit. It retains VSTest support for dotnet test. The mtp-off package excludes
Microsoft Testing Platform and its telemetry dependencies. No DI container, imaging library,
database, logging/telemetry package, or device SDK is introduced.

Restore explicitly uses the repository's NuGet.Config rather than per-user
configuration. The validation script confines caches and temporary files to
.local and overrides legacy extension-SDK probing to avoid inaccessible
per-user SDK directories. .NET targeting packs still come from the installed SDK.

Image formats, rendering rules, project file format, backup policy, and supported
device/Wallet workflows remain undefined. They require feature requirements and
security review before implementation. See the [threat model](../security/threat-model.md).
