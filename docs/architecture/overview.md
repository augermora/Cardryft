# Architecture overview

Cardryft is a modular .NET 10 Windows x64 application. Milestone 3 adds local
projects, state history, and responsive preview work to the offline editor.
The asInvoker application uses the installed Windows Desktop runtime.

## Dependency direction

App references Core, Imaging, Storage, Device, and Wallet. Imaging and Storage
reference only Core. Core uses no UI/platform APIs and performs no I/O. Device
references only Core; its 4B native backend remains disabled. Wallet remains empty.
Tests reference Core, Device, Imaging, Storage, and App; App and Device expose
internals only to the test assembly. All namespaces
start with Cardryft. No circular or infrastructure-to-infrastructure dependencies.

Milestone 4A is [Windows ↔ iPhone technical research](../research/windows-ios-device-access.md)
only. Milestone 4B follows its [preflight fallback](../research/windows-ios-device-access.md#milestone-4b-phase-1--preflight-fallback-2026-10-07):
managed boundaries exist, but native loading, communication and Wallet access remain disabled.

## Device preflight and read-only boundary

Core owns IDeviceDiscovery, immutable DeviceInfo/result snapshots and typed status,
trust and diagnostic values, with no platform API or I/O dependency. Public device
snapshots expose only four bounded optional metadata fields and no backend identifier.
Device owns the internal USB backend/owned metadata-connection interfaces, defensive
USB filtering/deduplication, normalized errors, and serial discovery orchestration.
There is no public IDeviceConnection or generic service/key/write/pairing interface.

NativeLibraryLoader is **preflight only**. It checks x64, rejects endpoint overrides,
network paths and reparse ancestors, and considers only an explicitly composed app
root's native/win-x64 directory. No DLL is loaded even if the directory exists:
MissingNativeBundle/UnreviewedNativeBundle are terminal until a reviewed runtime and
C ABI exist. No DllImport, unsafe pointer, native SafeHandle, socket or device call is
implemented. Consequently actual native handle disposal/ABI/timeout testing is deferred;
fake connection ownership is exercised with deterministic disposal on all paths.

LibimobileDeviceDiscovery always uses that unavailable backend in production. Its
internal test seam supplies USB snapshots and confirmed existing trust; it never
infers trust from metadata and queries only the four approved typed fields after
trusted/accessible status. Identifiers and deduplication state live only inside a
single refresh. Bounds are 32 candidates, 128 identifier characters, and 256 characters
per displayed value. Managed text checks do not replace future native allocation limits.

App composes discovery and owns DeviceRefreshController/DevicePanel. An explicit
Refresh serializes/coalesces work, cancels superseded requests, suppresses stale
results/errors, and awaits stop during window closure. No automatic polling or device
query occurs on startup. Device state never enters Storage, Imaging, project history
or recent lists. The editor remains usable when prerequisites are unavailable.

## State and history

ArtworkSession/ArtworkTransform/ArtworkSize are immutable validated records.
ArtworkSize.Canonical is the provisional 1024 × 640 Cardryft editor/export size;
it is not an Apple Wallet specification. Zoom is 1–4 and signed offsets are -1–1.
EditorDocument stores the current session, saved snapshot, and project identity.
Dirty state is value inequality against the saved snapshot, including undo/redo.
History stores up to 100 transforms, no raster or full-size image. Undo moves the
previous transform to redo; new edits invalidate redo; identical edits do nothing.
Reset uses the normal edit path. New/Open/import/replacement clear history.
Slider changes are individual steps; drag gestures are not grouped.

## Storage

ProjectStore serializes `.cardryft` JSON version 1: version, sourcePath, zoom,
horizontalOffset, verticalOffset, outputWidth, outputHeight. Fields are required;
unknown fields/versions are rejected. Read size is capped at 64 KiB, depth at 8;
Core validates all geometry. References must resolve to local PNG/JPEG paths.
No executable content, binary payload, polymorphic type activation, payment/device
metadata, or timestamps are stored. Save writes and flushes a unique sibling temporary
file before replacing the destination and cleans up on failure.

Storage normalizes source paths and saves references relative to the project directory
whenever both paths share a drive, using `/` separators and allowing `..`. Different
drives fall back to an absolute path. Load resolves relative paths against the project
directory, returning an absolute normalized path to the editor; drive-relative/rooted
but incomplete references are rejected. Existing absolute version 1 files remain valid.
The schema/version is unchanged; older absolute-only readers cannot read relative references.
Save As recomputes references against its destination. Moving both files preserves
portability only when their relative layout is maintained. No image is copied/embedded.
The source reference strategy avoids duplicate artwork but requires the image on reopen.
App retains project state if the referenced image cannot be decoded,
shows a warning, and permits replacement through Import Image. Replacement preserves
geometry, changes the source reference, and marks the session dirty. Failed JSON
loads/imports leave the old document/source intact. Save As changes project identity.

ApplicationDataPaths centralizes `%LOCALAPPDATA%\Cardryft\recent-projects.json`,
using Environment.SpecialFolder.LocalApplicationData. Its root can be injected;
RecentProjects also accepts an explicit file path so tests never write real AppData.
Default resolution is deferred until I/O; Read treats unavailable storage as empty,
and Add creates the directory on demand, reporting failures to App's nonfatal warning.
No administrator privileges or executable-directory writes are needed.
RecentProjects stores at most eight deduplicated project paths. Missing entries are ignored. Corrupt
lists are treated as empty; write failures are nonfatal warnings after successful
open/save. There is no image MRU, telemetry, cloud service, or roaming configuration.
No migration from the former executable-local list is performed. Project/recent
paths are plaintext; review projects before sharing because absolute fallbacks may
disclose local directories. Projects embed no image, payment, or device data.

## Imaging and ownership

ImageLoader checks PNG/JPEG extensions/signatures/dimensions before native decoding:
25 MiB encoded, 8192 per side, 32 million pixels. It verifies the decoded format,
normalizes EXIF orientation, and detaches pixels into a metadata-free 96-DPI bitmap,
closing the source stream. Core contains paths/values only; SourceImage owns pixels.

ArtworkRenderer owns all artwork geometry:

```text
scale = max(W / Sw, H / Sh) * zoom
left = (W - Sw * scale) / 2 + horizontalOffset * (Sw * scale - W) / 2
top  = (H - Sh * scale) / 2 + verticalOffset * (Sh * scale - H) / 2
```

It draws a bicubic crop into a rectangular 32-bit raster. Export uses this same
renderer and encodes a fresh PNG, preserving source alpha without paths/EXIF or
preview masks. ArtworkPreview scales the result proportionally and paints a rounded
UI shape with radius displayed height / 16. It never changes the source/result pixels.
Repeated output is deterministic on the same Windows raster stack, not necessarily
byte-identical across Windows/.NET versions.

## Async orchestration

ArtworkEditor owns document/source/preview and coordinates concrete storage and
imaging services. File decoding/parsing use Task.Run; only validated successful
results replace current state. UI menu/control mutations are serialized by the
WinForms synchronization context. File operations temporarily disable editing.

LatestPreview is an App orchestration helper with one async pump and one replaceable
pending session. Transform edits immediately change document state on the UI context.
The pump calls Task.Run for render, cancels superseded work, compares monotonically
increasing revisions, disposes stale bitmaps/errors, and publishes only current
results back on the UI context. No full-size source is cloned per slider event.
Cancellation is checked before/after GDI+ rendering; native calls cannot be aborted
mid-call. New/Open/replacement await StopAsync before replacing/disposal of pixels.
Close cancels/awaits the pump before editor disposal. During a file operation the
form remains open and asks the user to close again once it completes. Export is
serialized with file operations and disables editing while it waits
for previews, then uses synchronous render/encoding to avoid concurrent GDI+ access.

The preview can briefly show an older accepted frame while a newer request renders;
a stale completion can never replace the current frame. No image cache, DI container,
service hierarchy, background autosave, layers, or persistence of undo history.

## Packages, tests, and limitations

No Milestone 3 packages were added. Imaging uses System.Drawing.Common 10.0.12
(transitive Microsoft.Win32.SystemEvents 10.0.12). Tests retain Microsoft.NET.Test.Sdk
17.14.1, xunit.v3.mtp-off 4.0.1, xunit.runner.visualstudio 4.0.0. App/Imaging/tests
use net10.0-windows; Core/Storage/inactive projects remain net10.0. Tests exercise
round-trip/version/malformed storage, missing source, history/dirty state, recent
trimming, drop validation, controlled stale/canceled renders, and existing pixel/export
behavior. Device tests use fakes only; no runtime device/Wallet communication or
networking implementation exists. Native dependencies/versions and iOS 27 support
remain unvalidated; see the research document's 4B gates.

Native decoding is not sandboxed. Large sources can consume substantial bounded
memory; initial decoding temporarily holds old/new/native pixel buffers. Export/save
can briefly pause. Initial frame only, embedded profiles ignored, no source hash or
portable embedded bundles, autosave, backups, encrypted storage, layers,
undo grouping, or file relocation search. See the [threat model](../security/threat-model.md).
