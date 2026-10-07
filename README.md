# Cardryft

Privacy-first, open-source Windows x64 desktop editor for custom card artwork.
Cardryft works offline and does not interact with Apple Wallet, iPhones, devices,
private iOS protocols, payment credentials, telemetry, networking, or cloud services.

## Milestone 3: projects and editing

- Import or drop one local PNG/JPG/JPEG; invalid imports preserve current work.
- Rounded, resizable artwork preview with 100–400% zoom and readable signed pan
  percentages. Pan is a fraction of available crop travel; positive moves right/down.
- Reset, Undo (Ctrl+Z), and Redo (Ctrl+Y). History holds up to 100 transforms, not
  decoded images. Each slider change is one history step; a new edit clears redo.
  Import/replacement, Open, and New clear history. Reset is undoable.
- File > New (Ctrl+N), Open (Ctrl+O), Save (Ctrl+S), Save As (Ctrl+Shift+S), Exit,
  and Recent Projects. An asterisk in the title marks unsaved state. New/Open/Exit
  and replacing loaded artwork prompt Save / Discard / Cancel. Failed saves do
  not clear dirty state. Undoing to the saved snapshot clears it.
- Export a rectangular PNG through Save File. Rounded masking belongs only to
  the preview. Exports contain no chrome, borders, shadows, masks, or source metadata;
  genuine source transparency is retained.

ArtworkSize.Canonical centralizes the provisional **1024 × 640** editor/export
size (1.6:1). It offers a crisp landscape preview at modest memory cost. This is
Cardryft's convention, not an Apple Wallet-required size, and can be replaced
following future device/Wallet research. This UI uses the canonical size.

## Milestone 4B: device preflight (hardware blocked)

The sidebar now has a small USB-only Device section and an explicit Refresh button.
**Real iPhone detection and metadata queries are not enabled.** No reviewed x64
libimobiledevice DLL closure is available in the repository, so the request's
fallback is implemented: testable discovery/loader boundaries with a non-crashing
native-dependency-unavailable state. Adding arbitrary DLLs cannot enable this backend.
No libraries are loaded, device tools executed, phone connected, or pairing performed.

Fake-backed tests cover empty/multiple/duplicate/disconnected devices, trust and
restricted states, exactly DeviceName/ProductType/ProductVersion/BuildVersion,
connection disposal, cancellation and stale refreshes. These are not hardware or
iOS 27 compatibility results. Device identifiers are internal and transient;
artwork projects/recent lists never receive device state or metadata.

Preflight considers only `<application directory>\native\win-x64`; it rejects
non-x64, relative/UNC/device-namespace/network/reparse paths and any
USBMUXD_SOCKET_ADDRESS override. It reads no PATH or current-directory candidates,
changes no environment variables, and always fails closed pending native review.
Apple Devices or compatible Apple USB support must be separately installed by the
user for a future working backend; Cardryft will not install or redistribute it.
No new packages or native binaries were added; the MIT LICENSE is unchanged.

Before hardware testing, supply/build a provenance-verified x64 native set, audit
its recursive PE imports, exact versions/hashes/licenses, implement and review the
narrow C ABI and existing-trust path, then repeat automated validation. Only after
explicit instructions should the owner physically connect an iPhone. Current code
cannot perform that validation. See the [4B audit and manual gates](docs/research/windows-ios-device-access.md#milestone-4b-phase-1--preflight-fallback-2026-10-07).

## Project format and sources

`.cardryft` files are readable JSON, format **version 1**:

```json
{
  "version": 1,
  "sourcePath": "images/source.png",
  "zoom": 1.5,
  "horizontalOffset": 0.2,
  "verticalOffset": -0.1,
  "outputWidth": 1024,
  "outputHeight": 640
}
```

Projects reference the original image. Saving prefers a relative path from the
project's directory, including parent-directory (`..`) paths. Opening resolves it
against that directory, independent of the application's working directory. Moving
the project and image together preserves references if their relative layout stays
the same. Sources on a different drive use an absolute local path as a fallback.
Version 1 remains unchanged; existing absolute references still load. Older Cardryft
builds that only accepted absolute references cannot open new relative references.
No image content, arbitrary content, or payment/device data is copied or embedded.
Projects remain dependent on the original image; deleting/changing it affects reopening.
If it is missing or unreadable, Cardryft opens the project state with a warning;
Import Image selects a replacement while preserving transform and output size.
Replacement marks the project dirty and clears history.

Storage rejects unknown versions/fields, missing fields, invalid transforms/sizes,
non-image references, network paths, and project files over 64 KiB. Project saves
encode to a sibling temporary file, flush, and replace the destination; failures
preserve the previous file. Save As moves the current project identity to the new path.

Recent Projects keeps at most eight deduplicated project paths in
`%LOCALAPPDATA%\Cardryft\recent-projects.json`. Storage resolves Windows'
LocalApplicationData special folder and creates the Cardryft directory only when
writing, without administrator privileges. Tests inject isolated repository-local
paths instead of writing to the user's AppData. It stores no recent image
list. Missing entries are ignored; malformed/unwritable recent storage cannot
prevent successful project open/save. Previous executable-local lists are not migrated.
Project/recent files contain unencrypted local paths; projects may still disclose an
absolute path when a relative reference is impossible. Review `.cardryft` files
before sharing if local path disclosure matters. There is no Cardryft cloud synchronization.

## Rendering, safety, and limitations

Image loading and preview rendering run off the UI thread. One serial preview
worker has one replaceable pending request; newer edits cancel older work and
revision checks dispose stale results instead of displaying them. GDI+ native
calls are not interruptible mid-call; cancellation is checked around them.
Source replacement/New/Open await old preview work before disposing its image.
Closing awaits worker completion; during a file operation, close waits for the
operation to finish by keeping the window open and asking the user to close again.

One decoded source is reused for transform edits; there is no image history or
complex cache. Limits remain **25 MiB**, **8192 pixels per side**, and **32 million
pixels**. Imports release source file handles. EXIF orientation is normalized;
source DPI does not affect pixel geometry. Rendering uses cover scale → zoom →
bounded pan → bicubic rectangular crop. Preview presentation never mutates artwork.
PNG export awaits preview work and then renders/encodes synchronously for safe
image ownership; large exports can briefly pause the UI. JSON save/recent I/O is
also synchronous and small. Native decoding runs in-process, not in a sandbox.

Only the initial decoded frame is used; embedded color profiles are ignored.
No layers, rotation controls, text editing, history grouping, autosave, backups,
source relocation search, source integrity hash, or encrypted project storage.
File-picker navigation, filesystem links, and OS synchronization are outside the
application's controls; local-path checks do not comprehensively resolve reparse points.

## Build and validation

Requires Windows x64, .NET 10 SDK and Windows Desktop runtime/targeting support.
Normal NuGet connectivity is needed for initial restore; editing works offline.
From the repository root:

```powershell
.\scripts\validate.ps1
```

The script keeps CLI state/caches/temp files in ignored `.local`, disables CLI
telemetry, and executes:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Open `Cardryft.sln` in Visual Studio or launch after building:

```powershell
dotnet run --project src/Cardryft.App -c Release --no-restore
```

Core owns validated value state/history, Imaging owns raster operations, Storage
owns project/recent JSON, and App owns WinForms and orchestration. Device owns the
inactive native preflight and fake-testable USB boundary; Wallet remains empty.
No new packages were required for Milestones 3–4B; Imaging
uses System.Drawing.Common 10.0.12 and tests use xUnit v3.

See [architecture](docs/architecture/overview.md), [threat model](docs/security/threat-model.md),
and [contributor instructions](AGENTS.md). Licensed under the unchanged [MIT License](LICENSE).
