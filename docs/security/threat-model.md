# Threat model: Milestone 2

## Scope and assets

The application imports local PNG/JPEG artwork into memory, previews transforms,
and exports a full rectangular PNG to a user-selected local path. Rounded
preview masking is UI-only and does not remove exported corner pixels. The
provisional 1024 × 640 size is Cardryft's editor/export convention, not an
Apple Wallet requirement. Core validates immutable editor
state. No project persistence, backups, network clients, telemetry, external
process execution, device communication, or Apple Wallet access exists. Tests
use synthetic names and rasters; no payment/device records are collected.

Assets to protect are user artwork, in-memory source paths, user privacy,
repository integrity, and build dependency integrity. Artwork and display names
can themselves reveal personal information. Users should import artwork without
payment data; this editor does not inspect image content or redact sensitive pixels.

## Trust boundaries

- App accepts file paths and slider values; Core bounds zoom, offsets, and output sizes.
- Local file imports cross into the Windows GDI+ native decoder through Imaging.
- PNG export crosses into the user's filesystem through Imaging.
- NuGet restore crosses a development-time network boundary to obtain raster and test
  dependencies. It is distinct from application network communication.
- Device and Wallet boundaries are inactive and prohibited in this milestone.

## Threats, current controls, and remaining work

| Threat | Current control | Remaining risk or future requirement |
| --- | --- | --- |
| Collection or disclosure of payment/device secrets | No relevant data fields, integrations, pairing, network clients, or telemetry | Do not add card numbers, CVVs, PINs, bank credentials, or pairing storage |
| Malformed or resource-exhausting images | Extension/signature checks; PNG/JPEG dimensions inspected before decoding; 25 MiB encoded, 8192 per side, 32 million pixel limits; decoded format/dimensions checked; recoverable errors | Native decoding runs in process, not in a sandbox. Limits reduce allocation risks but cannot eliminate codec vulnerabilities; keep Windows/.NET updated |
| Source file locks or metadata disclosure | Import flattens pixels into a fresh bitmap, closes the stream, normalizes EXIF orientation, and does not copy source properties; output is encoded from a new raster | Paths remain in process memory. Image pixels may contain sensitive information; no content redaction is performed |
| Unsafe paths, accidental overwrite, or partial export | UNC/mapped network paths rejected; Save File prompts before overwrite; PNG-only output; encode to unique sibling temp then replace; failure cleanup | Filesystem links/reparse points are not comprehensively resolved. Exports are unencrypted user files and may be located in an OS-synchronized directory |
| Sensitive artifacts entering Git | Ignore rules cover local secrets, .env files, certificates, pairing/device directories, backups, and dumps | Ignore patterns cannot protect already tracked or differently named files; inspect diffs and untracked files |
| Elevated access | App manifest requests asInvoker; no privileged operations | Review any future capability that requests broader access |
| Dependency compromise | System.Drawing.Common 10.0.12 uses the Windows raster stack; required Microsoft.Win32.SystemEvents dependency; three pinned test packages; xUnit v3 mtp-off excludes Microsoft Testing Platform/telemetry dependencies | Pinning does not establish safety; review dependency changes and transitive packages |
| Accidental external tool or device execution | No device tools, private protocols, or downloaded binaries are invoked | Keep device and Wallet functionality inactive until explicitly scoped and reviewed |

## Restrictions and assumptions

Do not access Apple Wallet, connect to iPhones, request payment credentials,
process card numbers/CVVs/PINs/bank credentials, implement private iOS protocols,
store pairing records, introduce telemetry/application networking, or request
administrator privileges. Normal NuGet restore and .NET/xUnit validation are
allowed. Development writes stay inside C:\Dev\Cardryft; the validation script
keeps CLI state, NuGet caches, and temporary data in ignored .local directories.
No Git configuration/history changes, autonomous commits, pushes, or publication.

This milestone assumes a trusted local Windows account and installed .NET SDK.
It does not protect against a compromised host, sandbox native codecs, encrypt
exports, secure backups, or claim secure Wallet integration. File format checks
are not a guarantee that an arbitrary image is safe. Imports/renders are
synchronous; large images can temporarily pause the UI. Only the initial frame
is used, with no color-managed editing. No session history, project persistence,
autosave, cloud client, or backup feature is present. File-picker navigation and
OS file synchronization are outside Cardryft's control. Revisit this document
before expanding any input, storage, or integration surface.
