# Threat model: Milestones 3–4C preflight and artifact audit

## Scope and assets

Cardryft imports local PNG/JPEG into memory, previews transforms, saves/opens local
.cardryft JSON, stores recent project paths, and exports rectangular PNGs. No device,
Apple Wallet, payment, network, telemetry, cloud client, privileged access, or external
process execution exists. Development validation uses the allowed .NET/NuGet toolchain.
The provisional 1024 × 640 size is Cardryft's convention, not an Apple Wallet requirement.
Milestone 4B adds an inactive native preflight, fake-testable USB discovery and a UI
status section. The default backend cannot load libraries or communicate with devices.
Real-device validation is blocked on the exact native closure and audited interop.
Milestone 4C acquired project-owned MSYS2 archives as development audit data only,
checking published archive hashes and inspecting six extracted PE files without
loading/executing them. All data/tools/output stay under ignored repository-local
`.local/milestone4c-audit`. This development research is not application networking
or deployment. No production behavior/package/native execution boundary changed.

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
  dependency search restrictions and TOCTOU/loading risks remain a future gate.
  Candidate binaries/checksums now exist only as inert development audit evidence;
  no runtime binary is shipped or approved. Apple USB software remains separately
  installed; no proprietary Apple binary redistribution or elevation is authorized.
- Hardware use requires successful automated validation, explicit instructions and
  the owner physically connecting the test iPhone. The owner alone accepts trust through
  official UI if needed. Never reset existing trust merely to test untrusted behavior.
  Cardryft never initiates Pair or claims forensic proof of zero vendor/OS side effects.

An approved local usbmux IPC path would require a later reviewed backend; no sockets
are opened by this preflight. iOS 27 and driver/standard-user behavior are unverified.
The editor's existing boundaries and restrictions below remain in force.

### 4C provenance and runtime gates

The [native evidence manifest](../research/native-dependency-manifest.md) records
actual candidate hashes/imports, matching source recipes and incomplete approval.
Only named regular data entries were extracted using the existing Windows archive
tool. No downloaded installer/tool/native code was executed, no unpinned suite was
adopted and no proprietary Apple component/pairing directory/device was inspected.
Package hashes do not prove source-to-binary equivalence or exclude malicious code;
signature and independent rebuild verification remain incomplete.

The OpenSSL candidate enables dynamic zlib/engine support, relocates config/provider
directories and includes optional modules outside the six-file static PE closure.
Neither PE import parsing nor an absolute LoadLibrary path confines those loads.
Future builds must remove or explicitly validate them and prevent external config,
environment-driven modules, PATH/CWD/TEMP/Downloads, network and reparse/path escapes.
Hash pins require both protection against file-replacement races and a tested LGPL
source/rebuild/replacement route; see the [compliance plan](../../THIRD-PARTY-NOTICES.md).

Native receive code's initial timeout is not a total-operation bound: later receives
and advertised frame allocation need limits before an in-process backend can be
enabled. Do not abandon blocked native work or free modules/handles while it runs.
Actual ABI/SafeHandle/error-code/deadline tests remain deferred under the explicit
stop condition. No hardware validation or Wallet access takes place in 4C.

## Milestone 4D build and supply-chain boundary

### Milestone 4E runtime safety disposition

The [exact-source runtime audit](../research/native-runtime-safety.md) rejects
the 4D candidate. Reproducibility and static PE closure do not authenticate an
iPhone or bound hostile response allocations. The non-handshake path contains no
explicit Pair/record writes, but service/device side effects are not guaranteed.
Do not infer trust from record presence or a TLS handshake: peer verification is
disabled, security level is zero and plaintext StartSession can succeed.
Constructor DeviceClass access is outside the approved four-key scope.

Native mux/lockdown lengths are allocated before limits; parser, enumeration,
TLS and cleanup lack absolute operation deadlines. Cleanup can send StopSession
and await TLS shutdown, so finalizer I/O and abandoned C work are unacceptable.
Credential-bearing blobs must not enter logs, projects or public models; imported
buffers are heap-freed without secure wiping. A compromised loopback service,
changed transport environment or hostile device can cross this boundary despite
managed checks. Native endpoints must be immutable, USB-only and numeric-loopback;
checking an environment value once is insufficient.

No library, record or phone was accessed. No production loader/interop/SafeHandle
or promotion is added; new tests protect only the inactive managed boundary.
Manifest/PE/hash/race-resistant loading, native numeric error/lifetime tests,
pre-allocation budgets and the modified-library/app replacement route remain
blocked gates. Safety patches require new reproducible hashes, complete source
and license evidence, and hardware-free negative fixtures before any later
hardware milestone. The current runtime is **not ready for device use**.

Hash-pinned official source/toolchain archives are acquired under ignored
`.local/native-build`; the build runner has a controlled PATH and isolated
HOME/AppData/temp state. Offline builds execute compilers and source build scripts,
not device utilities. No package install hooks, global installation, administrator
privileges, application network communication or device calls are introduced.
Publisher HTTPS/hashes are bootstrap trust, not independently verified signatures
or a bootstrapped compiler. A compromised publisher/compiler remains a supply-chain
risk. Updating a pin requires review; unavailable/mismatched inputs fail closed.

OpenSSL's selected configuration removes configuration autoload, DSO modules, engines,
legacy modules and compression, including dynamic zlib; its socket BIOs are excluded.
The default provider is built in. This confines the selected initialization path,
not every possible public API in a general-purpose crypto library. Do not bind explicit
config/file/store/provider APIs. OPENSSL_CONF, OPENSSL_CONF_INCLUDE, OPENSSL_MODULES and
OPENSSL_ENGINES must not select
external files/modules through the approved path. Other environment controls (CPU
capabilities, diagnostics, certificate paths and transport overrides) still require
the future backend to use explicit options and reject unsafe overrides.
The MinGW RNG patch uses BCrypt's system-preferred OS RNG instead of registry-selected
legacy CryptoAPI providers; the build targets Windows 10 or later and links OS bcrypt.
This removes that provider-selection path without replacing OS entropy with a test RNG.

Static inspection requires an exact AMD64 file set, closed normal imports, no delay
imports and a narrow root export list. Hash equality from independent clean builds
is evidence for those outputs, not proof of memory safety, ABI or device compatibility.
No DLLs are copied to the application or loaded for discovery. Windows system
dependencies must later resolve through System32, never be collected from this machine.
Native CRT compatibility/dynamic resolution needs separate call-graph review.

The pinned libimobiledevice source explicitly sets TLS security level zero and disables
peer verification in its connection setup. The build does not add this behavior or
approve it: protocol authentication and existing-trust semantics must be reviewed
before use. Non-handshake construction performs extra reads; advertised frame sizes
and subsequent receives remain unbounded. Root export reduction does not enforce
USB-only arguments, four-key metadata or safe trust operations by itself. These
facts keep production interop and hardware use blocked. See the
[measured native gates](../research/native-dependency-manifest.md) and
[license/rebuild requirements](../../THIRD-PARTY-NOTICES.md).

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
