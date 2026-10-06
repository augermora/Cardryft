# Windows ↔ iPhone device access research

Research date: **2026-10-06**. The research phase was documentation only. No dependency
was added, device binary downloaded or loaded, third-party device tool executed,
iPhone connected, or device API exercised. Compatibility statements below are
source/document evidence or identified engineering inferences, not Cardryft
hardware results. This document does not authorize 4B or Wallet integration.

## 1. Executive summary

**Recommend a small Cardryft-owned P/Invoke adapter to upstream libimobiledevice,
conditional on Windows/iOS 27 experiments and native redistribution review.**
Use Apple's separately installed USB driver/service stack, USB-only enumeration,
and individual allowlisted lockdown information queries. Avoid archived bindings
and complete command-line tool bundles. First query without pairing or a session;
unavailable fields stay unavailable. If the required iOS-version query needs
authentication, validate a separately reviewed existing-trust session path before
promising that capability.

The convenience function `lockdownd_client_new_with_handshake` can initiate pairing
when no record exists. Do not use it here. Read-only behavior is a policy imposed
on a general-purpose native library, not a capability guarantee of that library.
[Pinned lockdown implementation](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/src/lockdown.c).

Apple documents iOS 27, and pymobiledevice3 has explicit iOS 27-related release
work. Neither proves a particular Windows DLL set supports our permitted queries.
No inspected candidate provides a verified Cardryft Windows x64/.NET 10/iOS 27
matrix. [Apple iOS 27 guide](https://support.apple.com/en-lamr/guide/iphone/iphe3fa5df43/27/ios/27),
[pymobiledevice3 releases](https://github.com/doronz88/pymobiledevice3/releases).

Windows usbmux uses loopback TCP IPC. Before 4B, explicitly scope permission for
that local IPC while retaining the external-network/Wi-Fi prohibition. Lockdown
is a reverse-engineered device protocol, not an official public Apple Windows SDK
or a Wallet API. Its narrow use also needs an explicit 4B policy review against
the existing private-protocol restriction. If all sockets or all nonpublic device
protocols remain forbidden, limit implementation to Windows PnP presence and defer
name/iOS queries; that does not fulfill the complete information goal.

## 2. Candidate comparison

These candidates overlap: upstream code, Windows distribution, and managed
integration are separate decisions. “Feasible” is an engineering inference, not
a tested integration. Maintenance observations reflect the inspected pages.

| Candidate | Maintenance evidence | Windows / iOS 27 evidence | .NET 10 and native requirements | Decision |
| --- | --- | --- | --- | --- |
| A. Upstream libimobiledevice | Latest displayed tag 1.4.0, 2025-10-10; Windows CI/source available | Upstream Windows target; no inspected release establishes our iOS 27 information matrix | C ABI via interop; native DLLs required | Preferred protocol implementation, conditional |
| B. Windows builds | libimobiledevice-win32 fork archived 2024-01-31; L1ghtmann release dated 2026-04-19; jrjr weekly release dated 2026-10-04 | Windows artifacts exist; freshness is not iOS 27 verification | P/Invoke feasible for correct x64 ABI; closure varies per build | Inspect recipes; no blind ZIP adoption |
| C. imobiledevice-net | Archived 2024-01-18; NuGet 1.3.17, 2021-02-22, deprecated | Historical Windows support; no iOS 27 evidence found | netstandard2.0 is consumable in principle; native assets/loader remain required | Reject new dependency |
| D. MobileDeviceSharp, alternative examined | Visible main history ends 2024-05-08; README says no unit tests | Wrapper around native stack; no Windows/.NET 10/iOS 27 matrix located | Managed integration plausible; native DLLs remain | Not established as maintained replacement |
| E. Direct P/Invoke | Cardryft owns a small adapter; upstream owns protocol code | Inherits selected native build's actual support | .NET native-loading/SafeHandle APIs available; x64 DLLs required | Preferred integration mechanism |
| F. Apple MobileDevice.dll directly | Apple maintains its product; no supported third-party Windows SDK contract located | Apple's product support does not validate private DLL exports | Interop technically plausible; proprietary native installation required | Reject private ABI coupling |
| G. Managed usbmux/lockdown + Apple transport | Cardryft would own protocol, plist and session/TLS implementation | Windows service protocol exists; iOS 27 behavior unverified | BCL implementation possible; Apple native transport still required | Defer excessive ownership |
| H. pymobiledevice3, reference only | Active releases, including v11.19.4 on 2026-09-27 and iOS 27-related changes | Documents Windows; iOS 27 evidence concerns other services, not these exact Windows queries | Python bridge/runtime needed; native crypto/Windows dependencies remain | Inspect, never embed/execute in this scope |
| I. Windows SetupAPI / Configuration Manager only | Documented Windows platform APIs | Host USB presence, not iOS version or verified iPhone identity | System DLL interop; no third-party native bundle | Optional degraded presence path |

Sources: [upstream release](https://github.com/libimobiledevice/libimobiledevice/releases/tag/1.4.0),
[Windows CI](https://github.com/libimobiledevice/libimobiledevice/blob/master/.github/workflows/build.yml),
[archived Windows fork](https://github.com/libimobiledevice-win32/libimobiledevice),
[L1ghtmann releases](https://github.com/L1ghtmann/libimobiledevice/releases),
[jrjr releases](https://github.com/jrjr/libimobiledevice-windows/releases),
[binding repository](https://github.com/libimobiledevice-win32/imobiledevice-net),
[NuGet metadata](https://www.nuget.org/packages/imobiledevice-net),
[MobileDeviceSharp history](https://github.com/mveril/MobileDeviceSharp/commits/main/),
[MobileDeviceSharp README](https://github.com/mveril/MobileDeviceSharp),
[.NET native loading](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/native-library-loading),
[Python platform notes](https://doronz88.github.io/pymobiledevice3/installation/),
[SetupAPI](https://learn.microsoft.com/en-us/windows/win32/api/setupapi/nf-setupapi-setupdigetclassdevsw).

### Trust, privilege, boundary and maintenance

Risk ratings are Cardryft's engineering assessment. Every information candidate
can use basic metadata queries without private Wallet APIs, but safe field
availability and authentication must be established on hardware. None is inherently
restricted to read-only operations.

| Candidate | Drivers / trust / administrator requirements | Security boundary / testability / risk |
| --- | --- | --- |
| A / E | Apple USB driver and compatible mux service on recommended Windows route. Pairing is separate from enumeration. Standard-user runtime is the target, unproven. | In-process C plist/TLS/transport parsing. Small managed seam can be faked; ABI/lifetimes need native/hardware tests. Moderate/high native upkeep. |
| B | Same transport/trust requirements; ZIP does not provide supported driver installation. User dependency install/repair may need elevation. | Provenance, mismatched DLLs and broad tool surface. Each artifact needs verification; high risk for blind redistribution. |
| C / D | Wrappers do not remove Apple/native prerequisites. Avoid pairing helpers; standard-user use needs validation. | C exposes test interfaces; D lacks documented unit tests. Stale loaders/ABI and large API surface make adoption high risk. |
| F | User-installed Apple components and independently established trust; no Cardryft driver installation or elevation. | Proprietary undocumented ABI/layout; mostly hardware tests. High SDK/distribution risk despite Apple's own maintenance. |
| G | Apple driver/service still needed; Cardryft owns session/trust handling. | Managed parsers can be fuzzed; protocol/credential correctness becomes ours. High implementation/maintenance cost. |
| H | Windows docs direct users to Apple software. Pair helpers may pair; developer/tunnel privilege needs are out of scope. | Broad Python dependency/service surface; .NET bridge/process deployment expensive. Good reference, poor application fit. |
| I | Host PnP enumeration needs no pairing; can indicate presence without functioning mux transport. Standard-user access must be tested. | Small OS interop, fake enumeration plus hardware-ID/driver tests. Low dependency burden; limited information. |

The Apple host prerequisite is documented in [libusbmuxd's Windows instructions](https://github.com/libimobiledevice/libusbmuxd#building).
USB presence is not trust, and protected service access depends on unlock state.
[Apple trust guidance](https://support.apple.com/en-gb/109054),
[physical pairing security](https://support.apple.com/en-gb/guide/security/secadb5b6434/web).
Apple's [AMDS article](https://support.apple.com/en-us/102347) confirms the service
dependency for iTunes, but does not prove every Apple Devices Store installation's
endpoint, layout or standard-user permissions; test those separately.

## 3. Recommended approach

### Narrow USB-only adapter

1. Enumerate using the selected native extended device list, reject network types,
   and create targets with USB-only lookup. Never implicitly select the first
   connected device. Revalidate its transient association on reconnect.
   [Pinned C device API](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/include/libimobiledevice/libimobiledevice.h).
2. Query on explicit user action through the non-handshaking lockdown client.
   Pinned creation also reads ProductVersion and DeviceClass. Allow only those
   plus individual DeviceName, ProductType and HardwareModel queries. No null-key
   whole-domain dump or arbitrary caller-supplied key/domain. Return nullable
   fields with availability reasons. Model values are raw technical strings;
   do not promise an up-to-date marketing-name mapping.
3. Stop on denied access. No fallback to a pairing helper. Successful basic
   information is not proof that the host is trusted.
4. If required fields need authentication, separately review a low-level existing-
   trust StartSession/StopSession path. Never generate HostIDs/certificates, call
   Pair/Unpair, run a trust-repair loop, or save/delete pairing records. Invalid
   existing authentication must fail closed. Audit the full transitive call graph
   before enabling this optional path.
5. Missing driver/native/ABI/license prerequisites disable information only; the
   editor remains functional. An optional PnP fallback labels unconfirmed Apple
   USB candidates honestly, including iPads and recovery/DFU modes. Never change
   recovery state or infer “ordinary connected iPhone” from vendor ID alone.

Pairing records are credentials. The native library can retrieve existing records
through usbmux for session authentication; that is a separate boundary from discovery.
Never copy them into projects/AppData/logs/backups, scan pairing directories, change
ACLs or create replacement records. Keep any necessary credentials in the internal
layer for the shortest lifetime and audit disposal; do not promise every native/TLS
heap is securely erased. [Pinned host-record source](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/common/userpref.c).

Read-only means no persistent device mutation, content extraction, installation or
trust change. Transport/session requests still change transient connection state;
the host/device may generate diagnostics. This is not a forensic zero-side-effect
claim.

### Native-library decision

For an authorized prototype, use a developer-selected reviewed native directory;
no automatic download or PATH discovery. For future releases, prefer a reproducible,
pinned, separately replaceable x64 DLL set built from upstream, bundled only after
provenance/license gates. Load lazily from an approved absolute directory with
controlled transitive DLL search. Missing dependencies must not prevent startup.

Discover Apple's installed transport as a prerequisite, without bundling Apple
DLLs/drivers/apps. Avoid arbitrary installed open-source DLL versions, static linking
and sealed single-file embedding. If compliant native distribution cannot be
maintained, defer full information rather than relaxing these requirements.

## 4. Rejected approaches and why

- **imobiledevice-net:** deprecated/stale native package; its documented sample uses
  the pairing-capable handshake. Computed NuGet net10 compatibility is not native
  safety or iOS 27 evidence.
- **Assuming MobileDeviceSharp is a maintained replacement:** inspected history and
  lack of tests do not establish current support. Its MIT wrapper does not remove
  native LGPL obligations.
- **Opaque Windows ZIPs:** artifact freshness and signed release commits do not
  establish binary provenance or licenses. jrjr's workflow clones floating upstream
  repositories and collects a broad tool/dependency set. [Build recipe](https://github.com/jrjr/libimobiledevice-windows/blob/main/.github/workflows/build.yml).
- **Private Apple MobileDevice.dll exports:** no supported Windows SDK contract
  located; proprietary ABI/redistribution dependence is fragile. Apple's installed
  service remains a transport prerequisite, not our programming SDK.
- **New managed protocol/TLS stack:** swaps native dependencies for ownership of
  certificate, session, parser and compatibility security; excessive for this scope.
- **Embedding/spawning Python or CLI tools:** runtime/bridge deployment, copyleft
  risk, broad mutation APIs and automatic-pairing defaults. Read pymobiledevice3
  for comparison; do not copy its implementation wholesale into MIT code.
  [Dependencies](https://github.com/doronz88/pymobiledevice3/blob/master/pyproject.toml),
  [pairing defaults](https://github.com/doronz88/pymobiledevice3/blob/master/pymobiledevice3/lockdown.py).
- **Replacement drivers/new daemon/libusb driver changes:** larger privileged
  deployment and possible Apple software disruption; unnecessary daemon/GPL surface.
- **Wi-Fi, tunnels, developer mode/DDIs, reverse proxies, exploits or jailbreak:**
  outside basic USB information. No current experiment uses any of these.

## 5. Proposed Cardryft.Device architecture

Proposal only; no types, project targeting or runtime behavior have been added.

| Project | Proposed ownership |
| --- | --- |
| Core | Small immutable DeviceInfo, DeviceConnectionStatus and IDeviceDiscovery contract; no Windows types, pointers, I/O, credentials or protocol implementation |
| Device | Concrete discovery/query orchestration, optional host-PnP fallback, dependency readiness, USB filtering, status/trust classification and native ownership |
| Device internal Native namespace | Explicit loader/import allowlist, SafeHandle lifetimes, bounded UTF-8/plist conversion, optional already-trusted session; invisible to App |
| App | Composition root, explicit query/selection UX, sanitized UI-facing state, synchronization and shutdown; no native calls or trust changes |
| Storage / Imaging / Wallet | No device dependency or device persistence; Wallet stays inactive |

One public IDeviceDiscovery operation returns an immutable read-only snapshot,
cancellable from App. It is justified by hardware isolation and fake-backed tests.
Begin with refresh/query, not a command bus. An internal concrete short-lived
connection owns handles. **Do not add public IDeviceConnection yet**: App has no
raw-session need. Reconsider only if multiple read-only operations justify a
separately testable connection lifecycle.

DeviceInfo contains a transient connection token, nullable name/ProductType/
HardwareModel/ProductVersion, status and per-field availability. Keep backend UDIDs
volatile only when necessary for correct target selection; do not query serial,
IMEI, account IDs or UniqueDeviceID merely to display information. No persistent
or hashed tracking identity; no device fields in artwork projects/recent lists.

Separate Present, InformationAvailable, AccessRestricted, TrustRequired (only with
evidence), UnsupportedMode and Disconnected from backend MissingDependency,
TransportUnavailable, AccessDenied and IncompatibleAbi. A service failure is not
“no phone”; a timeout is not “trust denied.” Unclassified errors remain unknown.

Use a single bounded worker/count/refresh rate, not uncontrolled tasks per hotplug.
Callbacks enqueue work and never query devices or touch WinForms. Compare generation
tokens before publication; await work before freeing handles. Keep delegates/native
modules alive until callbacks and users stop. [Windows notification API](https://learn.microsoft.com/en-us/windows/win32/api/cfgmgr32/nf-cfgmgr32-cm_register_notification).

Managed cancellation cannot abort a blocked C call. Audit native send/receive
timeouts and measure shutdown/disconnect bounds. Task.WhenAny does not suffice if
it leaves work running. No Thread.Abort or concurrent handle disposal. If native
calls hang indefinitely, hold release for a narrower solution; an external helper
process is outside this scope. Interop must match x64 layouts/calling conventions,
use SafeHandle and library-owned deallocators, and avoid incorrect CLR frees of
native memory. [.NET interop guidance](https://learn.microsoft.com/en-us/dotnet/standard/native-interop/best-practices).

## 6. Security analysis

Audit the allowlist's transitive behavior: list/connect/free, non-handshaking
creation, permitted GetValue and plist access/free initially; any separately
approved session path adds only its lifecycle. Do not expose generic send,
StartService, SetValue/RemoveValue, AFC/HouseArrest, installation, backup/restore,
activation, diagnostics, screenshots/logs, profiles, Pair/Unpair or Wallet APIs.
No payment credentials, device writes, apps, jailbreak or exploits.

| Threat | Proposed control and limit |
| --- | --- |
| Silent pairing | Never bind/call pairing helpers or repair trust. User independently manages trust through Apple software/iPhone. Unexpected trust prompt is a failed experiment, not permission. |
| Remote disclosure | USB-only lookup; no Wi-Fi/Bonjour/RSD. Refuse startup of the backend when USBMUXD_SOCKET_ADDRESS overrides the audited local default; no arbitrary remote/custom endpoint. |
| Local service spoofing | Verify loopback and expected Apple installation/service identity where reliable. This is risk reduction, not authentication of a compromised host. |
| Malformed replies | Bound frame/plist depth/count/string lengths and validate types; sanitize UI. Audit bounds before native allocations. Managed checks do not make C parsers memory-safe. |
| DLL planting | Approved absolute path, constrained dependency search, x64/export/provenance checks. No PATH/current-directory fallback. A directory being local does not make it trustworthy. |
| Privacy leaks | No protocol dumps, device names/UDIDs/pair blobs/TLS keys in logs, projects or recent lists. Only nonsensitive error classes/library versions in opt-in diagnostics. |
| Accidental mutation | Narrow imports/contracts and fake request traces. Whole DLL retains powerful exports: policy isolation is not a sandbox. |
| Resource/race failures | Bounded serial work, native timeout audit, stale-result suppression, awaited disposal. Native crash/hang risk needs hardware tests. |
| Privilege creep | asInvoker; no driver/service install, restart, ACL repair, elevation or passcode request in Cardryft. User dependency installation may independently require admin. |
| Vendor side effects | Separate Apple apps may sync/back up/contact services under their settings. Cardryft invokes none; future lab observation must distinguish vendor processes. |

Windows loopback and the remote environment override are visible in
[libusbmuxd source](https://github.com/libimobiledevice/libusbmuxd/blob/master/src/libusbmuxd.c).
Loader controls follow [Microsoft DLL security guidance](https://learn.microsoft.com/en-us/windows/win32/dlls/dynamic-link-library-security).
Current runtime boundaries are unchanged; update threat model/AGENTS before an
explicitly authorized 4B implementation.

## 7. Licensing analysis

Concrete distribution risks, **not legal guarantees**. Cardryft's MIT LICENSE is
unchanged. Review exact revisions, flags, per-file licenses and every shipped DLL.

| Component | Observed license / redistribution implications |
| --- | --- |
| libimobiledevice | LGPL-2.1-or-later in inspected C source; conditional redistribution, not MIT. Repository includes other license files; audit actual included code. |
| libusbmuxd client | LGPL-2.1; distinct from daemon. Its iproxy/inetcat tools are GPL-2.0 and unnecessary. |
| libplist / glue | LGPL-2.1 in READMEs; check selected code's exact variants/notices. |
| libtatsu | LGPL-2.1, TSS/curl-related; do not assume it belongs in runtime closure. |
| Open-source usbmuxd daemon | README identifies GPL-3.0, with multiple GPL license files present. Not bundled on Apple-service route. |
| imobiledevice-net | LGPL-2.1 wrapper plus separately licensed native assets. |
| MobileDeviceSharp | MIT wrapper; native LGPL obligations remain. |
| pymobiledevice3 | GPL-3.0; embedding/copying is not an MIT-only distribution strategy. |
| Apple components | Proprietary; no redistribution right established. iTunes Windows terms restrict redistribution; verify actual Apple Devices terms separately. User installs officially. |

Sources: [native source license](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/src/lockdown.c),
[client/tool license split](https://github.com/libimobiledevice/libusbmuxd#license),
[libplist](https://github.com/libimobiledevice/libplist#license),
[glue](https://github.com/libimobiledevice/libimobiledevice-glue#license),
[libtatsu](https://github.com/libimobiledevice/libtatsu#license),
[daemon](https://github.com/libimobiledevice/usbmuxd#license),
[binding](https://github.com/libimobiledevice-win32/imobiledevice-net),
[MobileDeviceSharp](https://github.com/mveril/MobileDeviceSharp),
[Python license](https://github.com/doronz88/pymobiledevice3/blob/master/LICENSE),
[iTunes Windows terms](https://www.apple.com/legal/sla/docs/iTunesWindows.pdf).

LGPL does not automatically require independently authored Cardryft code to abandon
MIT. Prefer replaceable shared libraries with prominent notices/license copies,
permission for user modification/debugging and compatible replacement. Changes to
LGPL code keep its license; static linking adds relinking-material obligations.
[LGPL 2.1 sections 2, 4–6](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/COPYING.LESSER).

Dynamic linking does **not** remove source obligations when Cardryft conveys DLLs.
Plan corresponding source/patch/build access for the exact shipped libraries, not
just an evolving upstream link. Loading a user-installed library differs from
redistributing it. [GNU static/dynamic FAQ](https://www.gnu.org/licenses/gpl-faq.en.html#LGPLStaticVsDynamic).

Proposed release packaging includes a per-file license/SBOM manifest and exact
native sources/build material alongside releases. Default provenance/hash checks
must not create a signature lock preventing explicit ABI-compatible modified LGPL
library replacement. Review loader, installer and crypto-license compatibility
before claiming compliance; do not assume every TLS provider/license combination
is interchangeable.

GPL code linked into an application differs from separate aggregation of tools;
neither dynamic linking nor a subprocess is a blanket copyleft exemption. MIT
files can retain notices while a combined GPL work may require GPL-compatible
distribution terms. Avoid unnecessary GPL daemon/CLI/Python integration and
wholesale source reuse. Independently review compiler runtime, crypto, compression
and vendored code. A builder's MIT badge cannot relicense its copied DLLs.

## 8. Deployment implications

Expected preferred runtime closure: libimobiledevice, libusbmuxd, libplist,
libimobiledevice-glue, one supported TLS/crypto implementation and actual compiler
runtime imports. DLL filenames/ABI vary by toolchain; inspect recursive PE imports.

Version 1.4.0 configure requires libusbmuxd ≥2.0.2, libplist ≥2.3.0, glue ≥1.3.0
and libtatsu ≥1.0.3 plus TLS. These are build minimums, not secure release pins or
proof every dependency ships at runtime. The core-library link recipe does not
list libtatsu directly. Measure actual imports; if TSS/curl components enter the
closure, include their licenses but prohibit their network/signing calls. Do not
ship the whole builder directory. [Pinned configure](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/configure.ac),
[core link recipe](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/src/Makefile.am),
[libtatsu purpose](https://github.com/libimobiledevice/libtatsu).

Prefer official Apple Devices provisioning if experiments pass; legacy iTunes is
a separately tested option. Do not promise identical stacks or simultaneous installs.
Installation/update can require elevation/connectivity; provisioned Cardryft must
operate as standard user without remote services or Apple-account credentials.
If a configuration needs runtime elevation, defer it. No Cardryft auto-download,
service repair, extracted Apple DLL, registry mutation, port-forwarder or daemon.
Only future Device Windows-specific code may need retargeting; Core stays portable.

## 9. Proposed Milestone 4B implementation plan

1. Explicitly authorize narrow USB queries, loopback IPC, native acquisition/builds
   and any existing-trust use. Current device/private-protocol restrictions remain
   until that milestone's review; no pairing feature is proposed.
2. Complete provenance/license/native and hardware gates below before selecting
   binary versions or advertising iOS 27 compatibility.
3. Add only Core snapshot/one discovery contract and Device's internal adapter,
   lifetimes, readiness and allowed queries; no generic service/command API.
4. Add explicit USB refresh/query/selection and sanitized status to App. Keep
   device data volatile, outside project/recent persistence.
5. Add fake-backend tests for USB filtering, absent fields, errors, multiple devices,
   reconnect identity, stale work, shutdown, dependency absence and allowed call
   traces. Hardware tests are opt-in; normal xUnit never connects to hardware.
6. Release basic information only after standard-user, no-pairing/no-mutation,
   no-remote-network and iOS 27 gates pass. Update threat model/AGENTS for that
   precise boundary. Completion never implies authorization for Wallet work.

## 10. Open questions / required experiments

All experiments are **future work**, not performed in 4A. Use an owned test iPhone
and explicit owner consent. Owner prepares dependencies/trust/settings separately;
Cardryft never changes them. Do not substitute generic CLI dumps/backups/diagnostics
for the audited query path. Record versions, nonsensitive error codes and timings,
not raw names, identifiers, credentials, whole plists or payment data.

| Experiment | Exact setup/action | Acceptance evidence |
| --- | --- | --- |
| E1. Binary/license audit | After separate authorization, build selected upstream x64 revisions in isolated lab; record source hashes/flags, recursive PE imports/exports, licenses and helper call graphs. | Attributable reproducible closure, supportable source/replacement packaging; no unexplained tools or invoked network/pairing initializers. |
| E2. Driver/service matrix | Windows 11 x64 standard user: compare no Apple stack, official Apple Devices, and separately legacy iTunes. Host-only inspect versions/service/endpoint. | Dependency absence recoverable; identify supported local mux endpoint and access without elevation. No Cardryft install/repair. |
| E3. iOS 27 field matrix | Record exact released 27.x build/model manually; USB with untrusted/unlocked, trusted/unlocked and trusted/locked phone. Query five permitted keys individually; repeat one prior iOS release. | Per-key availability/error table including ProductVersion; no generic-support inference, new trust prompt or persistent write. |
| E4. Pairing prevention | Owner prepares untrusted/revoked/invalid-existing-trust states; only non-handshaking discovery/queries. | No Pair/Unpair, repair or save-record trace. Unexpected trust prompt stops test; never accept it through Cardryft. |
| E5. Existing-trust session | Only if required by E3: prior owner-established Apple trust; audit session start, same queries, stop; repeat missing/expired record and locked phone. | Reuse or restricted status, no new HostID/certificate/record; inspect pair-store side effects. Defer if writes are unavoidable. |
| E6. Multiple devices/modes | Two devices; disconnect/reconnect during query; include iPad and owner-prepared recovery/DFU if safely available, without changing modes. | Correct target, stale suppression, unsupported classification, no recovery transition. |
| E7. Transport confinement | In isolated process set USBMUXD_SOCKET_ADDRESS to non-loopback test address; Wi-Fi-sync candidates present; then remove USB. Observe networking in separately authorized lab. | Refuse override before connection; no Wi-Fi fallback, external sockets, DNS, Bonjour, listener/tunnel or TSS traffic from Cardryft. |
| E8. Mutation audit | Source review plus instrumented permitted request/call traces; owner checks trust/files/apps before/after. | Only enumeration/connect, QueryType, permitted GetValue and approved session/free calls; no content/services/settings/Wallet/payment operations. Not a forensic zero-OS-side-effect proof. |
| E9. Lifetime/timeout | 100 connect/query/disconnect cycles; unplug in each native phase; owner stops service in lab; cancel/close during reads. | No leaks/crash/use-after-free or work after disposal; measured finite native timeout/shutdown bound, not just managed cancellation. |
| E10. DLL loading | Same-named DLL in working directory/PATH; isolated wrong-architecture/missing-export fixtures; explicit ABI-compatible modified LGPL module. | Unapproved candidates not loaded, no fallback, recoverable ABI errors, explicit lawful replacement works. |
| E11. Privacy/offline | External connectivity unavailable; inspect Cardryft-created project/AppData/log writes and process-local networking; distinguish Apple vendor processes. | No persisted device identity/name/credentials, no record duplication/creation, no remotely transmitted info, unchanged editor. |
| E12. Parser/resources | Fake malformed/oversized/non-string/invalid-UTF-8/control-character replies, excessive device counts and hung calls; native fuzzing only in authorized isolated lab. | Bounds before allocations where possible, sanitized UI, finite resources and recovery. Resolve missing native bounds rather than assuming managed checks suffice. |

Open gates: exact DLL/TLS/runtime closure and versions; Apple Devices endpoint and
standard-user permissions; unauthenticated field availability on iOS 27; whether
existing-trust sessions avoid pair-store side effects; native timeout/parser bounds;
and legal review of redistribution, crypto combinations and replacement loading.
No driver-free, all-iOS-version, privilege-free-installation or legal guarantee is made.

## Research validation

The original 4A research changed only this document and an architecture reference.
Application code, dependencies and tests were untouched at that stage. Ran
`.\scripts\validate.ps1` twice from
`C:\Dev\Cardryft`; its repository-local environment executes:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Both restores and builds succeeded; each build reported **0 warnings, 0 errors**.
The first test run reported **106 total / 104 passed / 2 failed / 0 skipped**:
`ArtworkEditorTests.FailedImportsAndProjectsPreserveCurrentState` and
`ArtworkRendererTests.Render_VerticalPanChangesCrop` failed in synthetic fixture
creation with `System.Drawing.Image.Save`'s null-encoder exception. The unchanged
full rerun reported **106 total / 106 passed / 0 failed / 0 skipped**. The initial
failure's root cause was unresolved at that stage; the finalization below addresses
it without changing production code. These tests do not validate device compatibility.

## Test stability finalization — 2026-10-07

### Root cause and evidence

The installed System.Drawing.Common/Windows Desktop version is **10.0.12**, build
commit `95017c711e6afc1085133d440e42b4bd78155701`. Its shared encoder lookup cache
publishes an empty array before filling the entries, without synchronization.
Concurrent first-use readers can miss PNG and receive Guid.Empty; the file-save
overload then throws ArgumentNullException with parameter `encoder`, before any
native encoding or file access. The ImageFormat value itself is not null.
The matching Microsoft [cache implementation](https://github.com/dotnet/winforms/blob/v10.0.12/src/System.Private.Windows.GdiPlus/System/Drawing/ImageCodecInfoHelper.cs)
and [save implementation](https://github.com/dotnet/winforms/blob/v10.0.12/src/System.Drawing.Common/src/System/Drawing/Image.cs)
explain the original exception and why an initialized-cache rerun passes.

An isolated repository-local .NET diagnostic reproduced the exact exception by
holding the installed cache in that published-but-empty state. Reflection mutation
was confined to that diagnostic process, never the test suite. A separate
100-wave/3,200-save cold-cache scheduling probe did not hit the narrow race window;
that does not contradict the deterministic state reproduction or the source defect.
The diagnostic added no packages or production dependencies.

Ownership review found each fixture creates its own bitmap and Graphics, completes
and disposes Graphics before saving, and disposes the bitmap afterward. No input
stream backs these dimension-created bitmaps. Fixture directories use independent
GUIDs; each fixture owns its cleanup. The exception occurs before file access, so
file collisions, cleanup or a disposed bitmap do not explain this encoder guard.
ImageFormat's singleton identifiers are immutable; the unsafe shared state belongs
to the framework's encoder cache. No broadly shared test bitmap was found.

### Exact fix and coverage

`ImageTestInitialization` uses a test-assembly module initializer to encode PNG,
JPEG and GIF once, into separate owned memory streams, before any xUnit test code
can execute. The first encode finishes the entire encoder cache initialization;
subsequent fixture saves and concurrently exercised application exports only read
the completed cache. Initialization failures propagate immediately. No exception
is caught, no operation retried and no assertion weakened. A fixture-local lock
would not protect the other Save/export call sites, so initialization is performed
at the test-assembly boundary. No framework internals are accessed by this fix.

**Test parallelism is unchanged.** No assembly/collection parallelism settings or
production behavior changed. `ImageTestFilesTests` adds PNG and JPEG cases with
16 independently owned fixtures released together on thread-pool workers. They
verify actual decoded format/dimensions/pixels, identical bytes, unique directories,
exclusive file access after encoding/decoding, deletion and directory cleanup.
All 106 prior cases remain; the complete suite now has 108 cases.

### Finalization validation

After the final code change, ran `.\scripts\validate.ps1` from the repository root,
executing `dotnet restore`, `dotnet build -c Release` and `dotnet test -c Release`.
Restore/build succeeded; **0 build warnings / 0 build errors**. Normal validation
passed **108 total / 108 passed / 0 failed / 0 skipped**.

Then ran `.\.local\encoder-probe\repeat-tests.ps1`, using the validation script's
same repository-local cache/AppData/temp settings. It executes exactly ten
consecutive full-suite commands, each in a fresh test host:

```powershell
# N is 01 through 10; each run has its own results file.
dotnet test -c Release --no-build --no-restore --logger "trx;LogFileName=run-N-final.trx" --results-directory C:\Dev\Cardryft\.local\validation\milestone4a-finalization
```

The loop stops immediately on any nonzero exit; it has no retry branch. No filters,
parallelism changes, failure suppression or selective reruns were used.

| Run | Total | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: | ---: |
| Normal validation | 108 | 108 | 0 | 0 |
| 01 | 108 | 108 | 0 | 0 |
| 02 | 108 | 108 | 0 | 0 |
| 03 | 108 | 108 | 0 | 0 |
| 04 | 108 | 108 | 0 | 0 |
| 05 | 108 | 108 | 0 | 0 |
| 06 | 108 | 108 | 0 | 0 |
| 07 | 108 | 108 | 0 | 0 |
| 08 | 108 | 108 | 0 | 0 |
| 09 | 108 | 108 | 0 | 0 |
| 10 | 108 | 108 | 0 | 0 |

Parsed all ten final TRX counter sets to confirm the recorded totals. Two earlier
normal-validation runs and the initial ten-run batch also passed 108/108 each.
The final batch above followed the adjustment that releases workers before directory
creation, so a failed fixture constructor cannot strand the start gate. No post-fix
suite run failed. Diagnostic source,
scripts and output are ignored under `.local/encoder-probe`; both batches of TRX files are
under `.local/validation/milestone4a-finalization`. The final diagnostic build also
reported 0 warnings/errors. Reviewable changes for this finalization are the two
new test files and this document; the earlier architecture reference is preserved.
No production code, packages, test-runner settings or Git history changed.

No further nondeterminism was observed in these runs. The upstream cache defect
still exists outside the test assembly; the fix establishes safe test initialization,
not a patched runtime or a production-behavior change. Hardware compatibility remains
unvalidated and no device work was performed.
