# Architecture overview

Cardryft is a modular .NET 10 Windows x64 application. Milestone 2 provides a
functional offline PNG/JPEG artwork editor with live pan/zoom and PNG export.
The executable is framework-dependent and uses the installed .NET 10 Windows
Desktop runtime. An explicit asInvoker manifest avoids elevation requests.

## Dependency direction

```text
Cardryft.App ----> Cardryft.Core
             |--> Cardryft.Imaging ----> Cardryft.Core
             |--> Cardryft.Device  ----> Cardryft.Core
             |--> Cardryft.Wallet  ----> Cardryft.Core
             `--> Cardryft.Storage ----> Cardryft.Core

Cardryft.Tests --> Cardryft.Core, Cardryft.Imaging, Cardryft.App
```

Core has no project or package dependencies and no WinForms or platform types.
Infrastructure libraries depend only on Core. There are no reverse references,
infrastructure-to-infrastructure references, or circular dependencies. Every
namespace starts with Cardryft. Shared build properties enable nullable reference
types, implicit usings, and x64 compilation. The SDK selector accepts stable
.NET 10 feature bands.

## Responsibilities and present behavior

- **App:** Program composes ImageLoader, ArtworkRenderer, and ArtworkEditor.
  ArtworkEditor coordinates session/pixel ownership and preserves the previous
  session if an operation fails. MainForm supplies file dialogs, sliders, and
  user feedback. ArtworkPreview displays the rectangular rendered bitmap inside
  an antialiased rounded shape, scaling its display rectangle proportionally on
  resize. This presentation mask never modifies the bitmap or performs artwork
  cropping/export. App owns the preview corner radius and placeholder decoration.
- **Core:** ArtworkProject remains intact. ArtworkSession, ArtworkTransform,
  and ArtworkSize are immutable validated records with get-only properties.
  They contain a source path, transform, and output dimensions without raster,
  UI, platform, payment, or device types. Core performs no I/O.
- **Imaging:** ImageLoader validates and detaches pixels. ArtworkRenderer owns
  cover scaling, zoom, crop positioning, rectangular raster rendering, and PNG encoding.
  SourceImage owns/disposes its raster. There is no WinForms reference or UI code.
- **Device:** Reserved for device abstractions. No discovery, pairing, or transport.
- **Wallet:** Reserved for customization abstractions. No Apple Wallet access.
- **Storage:** Reserved for project persistence and backup abstractions. No file I/O.
- **Tests:** Existing domain tests remain unchanged. Additional xUnit v3 tests
  cover transform boundaries/reset, loading failures/resource limits, source
  unlocking, EXIF orientation, rectangular/opaque export corners, source alpha,
  zoom/pan crops, dimensions, deterministic export, matching artwork pixels,
  failed export preservation, and preview masking without bitmap mutation.
  App exposes internals to the test assembly so an STA test can paint/resize the
  production WinForms preview control. This adds no production dependency.

Device, Wallet, and Storage intentionally have no implementations. No iPhone or
Apple Wallet access exists. Concrete image services are sufficient for this
milestone; no interface hierarchy or DI container is needed.

## Artwork geometry and rendering

ArtworkSize.Canonical centralizes the provisional Cardryft editor/export
**1024 × 640** canvas (1.6:1).
This approximate landscape artwork ratio and resolution provide a crisp desktop
preview with about 2.5 MiB per output raster; they are not an Apple Wallet asset
specification or Apple Wallet-required size. It can be replaced after future
device/Wallet research. Sessions can provide another validated landscape size (64–4096
pixels per side); the first UI deliberately exposes only the canonical size.
The preview-only corner radius is displayed card height / 16. Imaging has no
corner-radius or preview-mask concept.

For source size (Sw, Sh) and output (W, H):

```text
scale = max(W / Sw, H / Sh) * zoom
scaledWidth = Sw * scale; scaledHeight = Sh * scale
left = (W - scaledWidth) / 2 + horizontalOffset * (scaledWidth - W) / 2
top  = (H - scaledHeight) / 2 + verticalOffset * (scaledHeight - H) / 2
```

Zoom ranges from 1 to 4 relative to cover scale. Signed offsets range from -1
to 1, representing the available crop travel. Positive offsets move pixels
right/down; an axis without overflow does not move. The image always covers the
card bounds. Reset restores zoom 1 and zero offsets while preserving source/size.

ImageLoader accepts local PNG/JPG/JPEG only. It checks encoded length, signatures,
and dimensions before native decoding: 25 MiB, 8192 pixels per side, and 32
million pixels total. It validates the decoded format/dimensions, applies JPEG
EXIF orientation, then copies in explicit pixel coordinates into a fresh 32-bit,
96-DPI raster, avoiding display/source-DPI scaling. Render rasters also use
96 DPI and pixel units. The file/decoder stream
is closed after import; metadata and embedded color profiles are not retained.

ArtworkRenderer draws a bicubic crop to the full rectangular output raster.
Preview presents this same bitmap inside a rounded UI shape; export invokes
the same renderer and writes the complete rectangle as PNG. No radius mask,
border, shadow, chrome, or other preview decoration enters the export. Opaque
sources retain opaque corners; genuine source transparency is preserved.
Export encodes to a unique sibling temporary file before replacing the selected
destination, cleaning up on failure. Original source paths/EXIF are not embedded.
Repeated renders/exports are deterministic for the same pixels/state on the same
Windows raster stack; cross-version/platform byte identity is not guaranteed.

## Dependencies and deferred decisions

Imaging uses System.Drawing.Common 10.0.12, with Microsoft.Win32.SystemEvents
10.0.12 as a transitive dependency. It targets net10.0-windows to express the
Windows GDI+ requirement without referencing WinForms. App and raster tests also
target net10.0-windows; Core remains net10.0. The preview test uses the Windows
Desktop framework and references App. Tests require
Microsoft.NET.Test.Sdk 17.14.1, xunit.v3.mtp-off 4.0.1, and
xunit.runner.visualstudio 4.0.0; the adapter is a private test dependency.
The xUnit v3 test project is an executable, with its entry point supplied by
xUnit. It retains VSTest support for dotnet test. The mtp-off package excludes
Microsoft Testing Platform and its telemetry dependencies. No database,
logging/telemetry package, or device SDK is introduced.

Restore explicitly uses the repository's NuGet.Config rather than per-user
configuration. The validation script confines caches and temporary files to
.local and overrides legacy extension-SDK probing to avoid inaccessible
per-user SDK directories. .NET targeting packs still come from the installed SDK.

Rendering/import are synchronous and large images may briefly pause the UI.
The initial decoded frame is used; animation, color-managed editing, rotation
controls, layers, undo, project persistence, and backups remain deferred.
Device/Wallet workflows require separate requirements and security review.
See the [threat model](../security/threat-model.md).
