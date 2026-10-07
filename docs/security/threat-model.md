# Threat model: Milestones 3–4B preflight

## Scope and assets

Cardryft imports local PNG/JPEG into memory, previews transforms, saves/opens local
.cardryft JSON, stores recent project paths, and exports rectangular PNGs. No device,
Apple Wallet, payment, network, telemetry, cloud client, privileged access, or external
process execution exists. Development validation uses the allowed .NET/NuGet toolchain.
The provisional 1024 × 640 size is Cardryft's convention, not an Apple Wallet requirement.
Milestone 4B adds an inactive native preflight, fake-testable USB discovery and a UI
status section. The default backend cannot load libraries or communicate with devices.
Real-device validation is blocked on the exact native closure and audited interop.

Assets include artwork, paths, project edits, local files, privacy, and repository
integrity. Source/project/recent paths and artwork may disclose personal information;
project/recent JSON and exports are plaintext, not encrypted. The editor does not
redact sensitive pixels; users must not supply payment credentials or card data.

## Boundaries and controls

- Core validates geometry and holds bounded value-only history/saved snapshots.
- Imaging checks signatures/size/dimensions before in-process Windows GDI+ decoding.
  Limits remain 25 MiB, 8192 per side, 32 million pixels. Imports release source
  file handles; fresh rasters omit source metadata and normalize orientation/DPI.
- Storage treats project JSON as untrusted: 64 KiB/depth 8 bounds, required fields,
  exact version 1, unknown-field rejection, domain validation, and local image-only
  source references. No polymorphic deserialization or executable activation.
- Project persistence references original images, avoiding extra sensitive copies.
  Relative references resolve against the project directory and may include `..`;
  these are image references, not extraction destinations or embedded payloads.
  Absolute local paths remain a fallback when relative paths are impossible (different
  drives). Review `.cardryft` files before sharing if local path disclosure matters.
  No image content, payment data, device data, or identifiers are embedded.
  Missing/corrupt sources preserve project values and require explicit replacement.
  References are not immutable: an image changed on disk changes later reopening.
- Save/Save As and export encode to unique sibling temporary files before replacement;
  failures preserve prior destinations and clean up temp files. Save As and export
  use overwrite-confirming dialogs. Explicit Save intentionally overwrites the
  current project. Dirty state only clears after successful project save.
- New/Open/Exit/replacing loaded artwork offer Save / Discard / Cancel. History
  has 100 transforms, not image copies. File operation failure preserves current state.
- Recent projects retain eight local project paths only. Missing entries are ignored;
  corrupt lists cannot block use; unwritable storage is a nonfatal warning. There is
  no app cloud sync or roaming store. Storage uses Windows LocalApplicationData at
  `%LOCALAPPDATA%\Cardryft\recent-projects.json`, creating directories only on write
  without administrator privileges. Tests inject isolated repository-local paths.
  Data is not encrypted or coordinated across multiple concurrent instances.
- Preview work is serial/coalesced; cancellation/revision checks discard stale results.
  Source disposal waits for work completion. Native decoding/rendering cannot be
  interrupted mid-call; this is resource/lifetime control, not a codec sandbox.
- UNC/mapped network paths are rejected before file access. Reparse points/filesystem
  links are not comprehensively resolved; OS sync, remote-backed mounts not reported
  as network drives, file-picker navigation, and a compromised host remain outside
  the guarantee. User-selected files can be in an OS-synchronized directory.
- asInvoker manifest, no device tools/private protocols/pairing records, no network
  clients or telemetry packages. NuGet restore is development-only, not app networking.

## Remaining risks and restrictions

### Device boundary (currently inactive)

- Only explicit Refresh checks prerequisites. NativeLibraryLoader does not call an
  OS loader, search PATH/CWD, connect to usbmux or inspect Apple pairing directories.
  Its app-root/native/win-x64 location is centralized inside Device. Non-x64, UNC,
  device-namespace, mapped network and reparse paths and endpoint overrides fail closed.
  Directory/file presence is not provenance; no unreviewed bundle can enable the backend.
- Fake discovery rejects network/unknown transports and duplicate candidates, limits
  counts/identifier lengths and queries exactly DeviceName, ProductType, ProductVersion
  and BuildVersion only with confirmed existing trust and accessible status. Neither
  a successful metadata value nor USB presence proves trust. Unavailable fields stay
  unavailable; restricted/unknown states are not treated as trust permission.
- No pairing, record creation/deletion/repair, generic lockdown dictionary, StartService,
  Wallet, payment, media, contacts, messages, filesystem, app enumeration or write API
  exists in the boundary. No network clients, Wi-Fi fallback, external helper or telemetry.
- Public snapshots exclude identifiers; metadata is UI-only and transient. Generic
  error/ToString results exclude names/IDs/raw responses. No device state is sent to
  Storage, recent JSON, artwork projects, exports or logs. All tests use synthetic data.
- Serial/coalesced refresh and awaited shutdown protect fake connection lifetimes;
  IDisposable tests do not prove native SafeHandle or ABI correctness. Before real
  interop, audit transitive calls, allocation bounds, finite native timeouts and
  calling conventions; managed cancellation cannot abort blocked C calls.
- DLL hashes, signatures, recursive import closure, source/replacement obligations,
  dependency search restrictions and TOCTOU/loading risks remain a future gate. No
  native binaries/checksums are introduced now. Apple USB software remains separately
  installed; no proprietary Apple binary redistribution or elevation is authorized.
- Hardware use requires successful automated validation, explicit instructions and
  the owner physically connecting the test iPhone. The owner alone accepts trust through
  official UI if needed. Never reset existing trust merely to test untrusted behavior.
  Cardryft never initiates Pair or claims forensic proof of zero vendor/OS side effects.

An approved local usbmux IPC path would require a later reviewed backend; no sockets
are opened by this preflight. iOS 27 and driver/standard-user behavior are unverified.
The editor's existing boundaries and restrictions below remain in force.

Keep Windows/.NET patched; native codec vulnerabilities are not eliminated by
format/resource checks. Initial decoding can hold multiple bounded pixel buffers;
large images consume memory and GDI+ work may take time. UI decoding/preview is async,
while export and small JSON writes remain synchronous. Filesystem race conditions,
concurrent application instances, source integrity hashing, autosave, crash recovery,
secure backups, encrypted projects, and portable project bundles are not addressed.

Preserve repository ignores for secrets/.env/certificates/device/pairing/backups/dumps;
inspect all diffs/untracked files. CLI/cache/temp state remains in repository .local;
recent runtime data uses the user's LocalApplicationData directory. Development
validation writes only repository-local fixtures/caches, never real user AppData.
No Git history,
configuration, commits, pushes, or publication changes. Revisit the model before
adding new formats, storage surfaces, services, device, or Wallet integration.
