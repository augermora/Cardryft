# Cardryft
Privacy-first, open-source Windows application for creating and managing custom Apple Wallet card artwork.

## Milestone 2: offline artwork editor

Import a local PNG, JPG, or JPEG, adjust zoom and horizontal/vertical position,
reset to centered cover, and export the rectangular artwork as a PNG. The preview
updates immediately and maintains its landscape aspect ratio when resized.
Cardryft does not yet interact with Apple Wallet or devices. There is no
telemetry, application networking, cloud storage, or administrator requirement.

### Editing and export

- **Import Image:** chooses a local PNG/JPEG. The source is copied into memory
  and is not locked after import; importing a replacement starts a new session.
- **Zoom:** 100–400%, relative to the initial scale that covers the canvas.
- **Position:** -100–100% of the available crop travel on each axis. Positive
  offsets move the image right/down. An axis without overflow has no travel.
- **Reset:** restores 100% zoom and zero offsets without changing the source.
- **Export PNG:** chooses a destination through Save File; exports artwork only,
  as a full rectangle, with no preview mask, UI overlays, or copied source metadata.
  Transparency is retained only where it comes from the source artwork.

The provisional Cardryft editor/export size is **1024 × 640 pixels** (1.6:1). This approximate
landscape card ratio offers a crisp desktop preview at modest memory cost and
is twice a 512 × 320 display size. It is an artwork convention, not an Apple
Wallet asset specification. The size is centralized in Core's ArtworkSize.Canonical
and configurable through ArtworkSession; this first UI uses the canonical size.
It can be replaced after future device/Wallet research; it is not an Apple
Wallet-required size.

Preview and export share the same renderer: centered cover scale → zoom → bounded
pan → bicubic crop → rectangular raster. App paints this raster inside an
antialiased rounded preview shape without changing its pixels. Export encodes
the full raster, including its corners. PNG transparency is preserved;
JPEG EXIF orientation is applied before editing. The decoder ignores embedded
color profiles and does not copy metadata into the new raster or output PNG.

### Current limits

Images must be at most 25 MiB, 8192 pixels per side, and 32 million pixels total.
Unsupported, corrupt, oversized, missing, or inaccessible files show a recoverable
error and preserve the current session. UNC and mapped network paths are rejected.
Native Windows image decoding is not a security sandbox. Imports/renders are
synchronous; large images may briefly pause the UI. Animated/multipage editing,
color-managed editing, rotation controls, text, layers, undo, project persistence,
and backups are not implemented. Only the decoder's initial frame is used.

## Requirements and validation

- Windows x64 with a .NET 10 SDK and Windows desktop targeting support.
- NuGet connectivity for the initial dependency restore; editing then works offline.

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

Open `Cardryft.sln` in Visual Studio with .NET 10 support, or launch the editor
after validation with:

```powershell
dotnet run --project src/Cardryft.App -c Release --no-restore
```

## Projects

| Project | Responsibility |
| --- | --- |
| `src/Cardryft.App` | WinForms UI and application composition root |
| `src/Cardryft.Core` | UI-independent domain models, validation, and future shared contracts |
| `src/Cardryft.Imaging` | Bounded image loading, raster rendering, scaling/cropping, and PNG export |
| `src/Cardryft.Device` | Future device abstractions; no communication |
| `src/Cardryft.Wallet` | Future Wallet customization abstractions; no Wallet access |
| `src/Cardryft.Storage` | Future project persistence and backup abstractions |
| `tests/Cardryft.Tests` | Domain, raster pipeline, and WinForms preview tests using xUnit v3 |

Device, Wallet, and Storage remain project boundaries without implementations.
Core is independent; Imaging has no WinForms code. The only production package
is System.Drawing.Common 10.0.12 for Windows raster graphics.

See [architecture](docs/architecture/overview.md), the
[threat model](docs/security/threat-model.md), and [contributor instructions](AGENTS.md).

## Security and license

Cardryft handles artwork, not payment credentials. Do not supply card numbers,
CVVs, PINs, or bank credentials. Cardryft does not access Apple Wallet or
iPhones, implement private iOS protocols, or store pairing records. Local
secrets, certificates, device data, backups, and diagnostic dumps are excluded
from Git; ignore rules are not encryption or a substitute for careful review.

Licensed under the [MIT License](LICENSE).
