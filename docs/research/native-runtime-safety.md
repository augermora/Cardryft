# Milestone 4E: runtime safety and ABI audit

Audit date: **2026-10-08**. **STOP: the Milestone 4D candidate is not approved
for loading, promotion, redistribution as an enabled runtime, or hardware use.**
Production behavior remains the inactive 4B preflight. No iPhone, Apple service,
pairing directory, or pairing record was accessed. No native DLL was loaded or
executed. No native builds were repeated. All observations below are static
source/header/PE inspection; negotiated TLS, device behavior and executable ABI
conformance have not been tested.

## Decision and evidence

The request explicitly requires stopping if the native API cannot guarantee the
pairing-record restrictions. The narrow source path contains no explicit Pair,
SavePairRecord or DeletePairRecord request, but it does not establish a guarantee
about side effects inside Apple's opaque service/device. It also lacks an approved
existing-trust constructor and authenticated peer proof. Independent metadata,
allocation, deadline and cleanup blockers make enabling the exact 4D hashes unsafe.
Accordingly production loader/P/Invoke/SafeHandle implementation and DLL promotion
are deferred under that stop condition. This is a completed static assessment,
**not a successful runtime-safety gate**.

| Gate | Result for the exact 4D candidate |
| --- | --- |
| Reproducibility / static dependency identity | E/F evidence retained; 12 staged DLL instances rehashed and PE imports/exports compared with 4D; all match |
| TLS peer authentication / existing trust | Failed: verification disabled, security level 0, plaintext session can succeed; record presence is not confirmed trust |
| Pairing-record immutability | No explicit record writes on the traced primitives; vendor/device side effects unproven; automatic handshake helper forbidden |
| Four-key metadata boundary | Failed natively: constructor adds DeviceClass and reads ProductVersion before confirmed trust |
| USB / fixed local endpoint | Managed preflight rejects overrides, but native environment redirects and network snapshots remain available |
| Native allocation / absolute deadlines | Failed: advertised lengths allocated before limits, unbounded enumeration and read/handshake loops |
| ABI / ownership | Header declarations and release paths documented below; runtime layout, failure ownership and bounded cleanup not validated |
| Production loader / replacement route | Deferred; preflight always unavailable, no production manifest or release pin generator |
| Hardware readiness | **No** |

Inputs are the five pinned commits and seven patches in
[sources-lock.json](../../native-build/sources-lock.json), frozen E/F recipes and
[4D evidence](../../native-build/evidence/milestone4d-results.json).
The exact inspected files are under `.local/native-build/runs/E/src`; key source
and header hashes were also compared with F and the hash-locked original archives
(all ten match). Selected archive members were extracted as inert text into a
fresh `.local/milestone4e-source-check`, preserving existing work.
Fresh, non-executing verification is
recorded in [4E evidence](../../native-build/evidence/milestone4e-results.json).
The existing 62-file E source-material inventory still matches, contains eight
source archives and no DLL/EXE/PDB, and was not regenerated or overwritten.

Primary source references (line numbers below refer to these pinned files):

- [idevice.c](https://github.com/libimobiledevice/libimobiledevice/blob/149f7623c672c1fa73122c7119a12bfc0012f2ac/src/idevice.c)
  and [device header](https://github.com/libimobiledevice/libimobiledevice/blob/149f7623c672c1fa73122c7119a12bfc0012f2ac/include/libimobiledevice/libimobiledevice.h).
- [lockdown.c](https://github.com/libimobiledevice/libimobiledevice/blob/149f7623c672c1fa73122c7119a12bfc0012f2ac/src/lockdown.c)
  and [lockdown header](https://github.com/libimobiledevice/libimobiledevice/blob/149f7623c672c1fa73122c7119a12bfc0012f2ac/include/libimobiledevice/lockdown.h).
- [userpref.c](https://github.com/libimobiledevice/libimobiledevice/blob/149f7623c672c1fa73122c7119a12bfc0012f2ac/common/userpref.c)
  and [property_list_service.c](https://github.com/libimobiledevice/libimobiledevice/blob/149f7623c672c1fa73122c7119a12bfc0012f2ac/src/property_list_service.c).
- [libusbmuxd.c](https://github.com/libimobiledevice/libusbmuxd/blob/adf9c22b9010490e4b55eaeb14731991db1c172c/src/libusbmuxd.c)
  and [usbmuxd.h](https://github.com/libimobiledevice/libusbmuxd/blob/adf9c22b9010490e4b55eaeb14731991db1c172c/include/usbmuxd.h).
- [plist header](https://github.com/libimobiledevice/libplist/blob/cf5897a71ea412ea2aeb1e2f6b5ea74d4fabfd8c/include/plist/plist.h)
  and [plist.c](https://github.com/libimobiledevice/libplist/blob/cf5897a71ea412ea2aeb1e2f6b5ea74d4fabfd8c/src/plist.c).

Build patches change build/export selection, library spelling, OpenSSL informational
paths and RNG sourcing. They do not change the protocol functions audited here.

## Exact call path, trust and record behavior

The proposed path, **never executed**, is:

```text
idevice_get_device_list_extended
  -> usbmuxd_get_device_list -> ListDevices (or legacy Listen fallback)
filter USB, bounded transient identifier
idevice_new_with_options(identifier, IDEVICE_LOOKUP_USBMUX = 2)
  -> usbmuxd_get_device -> another device list; creates device, version/class = 0
lockdownd_client_new(device, ..., fixed label)
  -> property_list_service_client_new -> service_client_new -> idevice_connect
  -> usbmuxd_connect(device ID, port 62078)
  -> QueryType -> GetValue(ProductVersion) -> GetValue(DeviceClass)
existing HostID acquisition [no approved narrow entry point in the nine exports]
lockdownd_start_session(client, existing HostID, NULL, &ssl_enabled)
  -> ReadBUID -> StartSession -> read SessionID / EnableSessionSSL
  -> if SSL requested: userpref_read_pair_record -> ReadPairRecord -> TLS BIO
four GetValue requests with Domain omitted and a non-null allowlisted Key
lockdownd_client_free -> StopSession -> TLS shutdown -> transport/heap cleanup
idevice_free; free owned result plists and enumeration
```

`lockdownd_client_new` (615–697) is not the automatic-handshake helper. However,
it makes pre-trust metadata reads and returns success even when QueryType or the
implicit reads fail. It is not proof of accessibility, authentication or trust.
Never pass network option bits or a null identifier to select an arbitrary device.

`lockdownd_client_new_with_handshake` (699–797), excluded from the nine root
exports, calls Pair when a record is absent, and may pair again on InvalidHostID.
Pair/Unpair use record save/delete at lockdown.c 1006/1024. Those operations and
record generation must remain unreachable. Excluding exported names does not
remove all internal general-purpose source, nor restrict dependency exports.

`lockdownd_start_session` (1158–1245) consumes HostID and a system BUID obtained
through `userpref_read_system_buid` -> `usbmuxd_read_buid`. The SSL path reads an
existing record using `userpref_read_pair_record` (323–356) ->
`usbmuxd_read_pair_record` (1672–1716). These send ReadBUID/ReadPairRecord to the
local USB service; they do **not** directly open `%PROGRAMDATA%\Apple\Lockdown`.
That location appears in other userpref helpers, not this selected record-read
path. The actual backing storage and vendor-service mutations are outside the
audited source and not verified here. Cardryft must not inspect or copy that store.

ReadPairRecord returns the complete credential-bearing PairRecordData blob, parses
it natively and duplicates key buffers. In the selected OpenSSL branch,
`RootCertificate` and `RootPrivateKey` are consumed (idevice.c 1222–1223), not the
HostCertificate/DeviceCertificate fields. An existing HostID must also be obtained
from existing material; `lockdownd_start_session` does not obtain it for the caller.
The root's nine exports provide no narrow authenticated existing-record session
constructor. Exposing whole credential dictionaries to managed code is not an
approved workaround; a future reviewed native boundary must minimize and wipe
temporary secrets and have explicit bounded ownership.

There is no explicit record save/delete or certificate generation/persistence in
the selected non-handshake/start-session/TLS path. Session IDs and device version/
class are mutated in process memory; StartSession/StopSession change transient
protocol session state. No source evidence proves they write persistent device
files, and no source evidence guarantees Apple's service/device never changes its
own bookkeeping. **Neither a claim of automatic pairing on this narrow path nor
a guarantee of zero vendor side effects is justified.** Record data is heap-freed,
not securely wiped; BIO/context allocation failures can leak imported key buffers.

### TLS authentication findings

The selected OpenSSL 3.6.5 path creates `SSL_CTX(TLS_method())`, sets security
level **0** (1243), minimum TLS 1.0 (1261), maximum TLS 1.0 for detected versions
below 10, and minimum 0 when the version is unknown (1276). For this OpenSSL build
it also enables IGNORE_UNEXPECTED_EOF and LEGACY_SERVER_CONNECT (1287/1294).
These are existing upstream policies, not Cardryft build additions; none are approved.

It supplies the root certificate/private key for client authentication. Failed
certificate/key attachment only logs a warning. `SSL_set_verify(ssl, 0, ...)`
(1338) disables peer verification; the callback (1120–1123) always returns 1.
No comparison with the paired device's certificate is made on this path. A
successful SSL handshake alone therefore cannot establish authenticated device
identity. A compromised loopback service or redirected endpoint is in scope.
An authentication fix requires protocol evidence and offline positive/negative
fixtures, not simply toggling a web-PKI verification flag or lowering TLS policy.

StartSession accepts missing/false EnableSessionSSL and returns success without
TLS. Missing SessionID can also leave a success result. Missing/invalid arguments
are not safely rejected before client dereference (1163–1167). A future boundary
must fail closed on absent TLS/session/credentials, malformed responses, failed
peer authentication and any unsupported state. No negotiated protocol/version,
cipher or trust behavior has been observed on hardware.

| Device state | Required future policy / current evidence |
| --- | --- |
| Existing trusted, accessible | Only return Trusted after authenticated existing-record session success; current candidate cannot prove this |
| No/invalid record or untrusted | Return NotTrusted/unknown with no metadata; no Pair, repair, prompt automation, or retries that create records |
| Locked/restricted | Return Restricted or unavailable according to validated errors; never bypass a restriction or change trust |
| Disconnected/service unavailable | Return NoDevice/TransportUnavailable without metadata; bounded cleanup on every failure |
| Malformed/unknown native result | Fail closed, generic diagnostic, no credentials/identifier in output |

## Metadata and protocol dictionary inventory

Only **DeviceName, ProductType, ProductVersion, BuildVersion** are approved
device GetValue keys. Managed `DeviceMetadataField` already has exactly these
four values. Domain must be null/omitted and Key must be one of those non-null
UTF-8 constants; null Key omits the filter and requests a dictionary. No generic
key/domain or Pair API may escape the internal interop layer.

Current constructor reads **ProductVersion and DeviceClass**, before confirmed
trust, when QueryType matches. DeviceClass is outside scope and is a failed gate,
not a newly approved key. ProductVersion may be read again explicitly. The four
explicit requests would produce five distinct device keys with this constructor.
No serial/IMEI/ECID/phone/Apple-ID/apps/Wallet key is requested in this traced
path; arbitrary GetValue remains possible at the C ABI and must never be exposed.

Protocol fields are also parsed: QueryType's Type; response Request/Result/Error
and Value; StartSession's SessionID/EnableSessionSSL; StopSession results. These
are bounded protocol envelopes, not permission to fetch device dictionaries.
Host IPC parses MessageType/Number, DeviceList/Properties, DeviceID, ProductID,
ConnectionType, and the host property named SerialNumber (libusbmuxd.c 269),
which this implementation uses as the **technical UDID**. It is not a lockdown
SerialNumber query. That identifier is permissible only transiently for connection
management, never UI, logs, projects or recent files. Native network entries also
parse NetworkAddress before managed filtering. Record IPC parses BUID and
PairRecordData; credentials are authentication material, not displayable metadata.

## USB and endpoint policy

Windows' default IPC is **TCP 127.0.0.1:27015**, from libusbmuxd.c 222 and
usbmuxd-proto.h 30. This local transport to Apple's service is distinct from
connecting to a network iPhone. It is not cryptographic service authentication.
`connect_usbmuxd_socket` (164–229) reads USBMUXD_SOCKET_ADDRESS on every socket
open and can connect to an arbitrary hostname/IP/port, including IPv6. UNIX:
overrides are ignored on Windows; they remain forbidden configuration anyway.

Extended enumeration includes USB **and network** devices. USB-only open option
2 avoids direct network device connection, but re-enumerates the broader host
list. ListDevices has no USB-only filter here, and legacy Listen can stream an
unbounded number of events. Do not subscribe to asynchronous discovery or start
the GNU usbmuxd daemon. The library has shared protocol/tag state; a future native
boundary needs serialization across instances, not only per-discovery semaphores.

Current production never opens a socket. Preflight rejects **any defined**
USBMUXD_SOCKET_ADDRESS, including empty or loopback overrides, without mutation.
It captures the value when composed; that is not a race-proof restriction if
native code later re-reads a changed environment. A future reviewed native path
must use an immutable fixed numeric endpoint at each connect, with no hostname,
alternate endpoint, Wi-Fi option, or environment fallback. Do not clear global
environment variables to paper over this. Reject unknown connection kinds,
filter USB before connection and metadata, and retain no network addresses.

## Limits: implemented versus required

These proposed native limits are initial engineering targets, not implemented or
proved by managed tests. Incompatible responses must fail closed, not enlarge them
silently. All conversions/multiplications/additions and header subtraction need
checked bounds before allocation, parsing, copying or marshalling.

| Data / work | Current managed boundary | Required future native policy |
| --- | --- | --- |
| Device count/enumeration | Maximum 32 candidate objects after materialization | At most 32 parsed entries/events before allocation; bounded terminator validation; no unlimited Listen stream |
| Identifier | Nonblank, at most 128 UTF-16 code units | At most 128 ASCII bytes, no embedded NUL/control; borrowed scan/copy bounded before decoding |
| Display string | At most 256 UTF-16 code units, controls sanitized | At most 1024 UTF-8 bytes before strict decoding, then at most 256 UTF-16 code units; reject NUL and invalid UTF-8 |
| Host/session IDs | No native implementation | At most 128 UTF-8 bytes each, internal only; malformed or absent session fails |
| Lockdown/mux frame and pair blob | No native cap | At most 1 MiB per frame/record, checked minimum header and exact reads before allocation |
| Parsed plist | No native cap | At most depth 16 / 4096 nodes, aggregate string/data and heap budget; envelope/type validation before Value copy |
| Native allocation | No enforced budget | At most 1 MiB single response buffer and 8 MiB aggregate operation-owned allocations, including parser/TLS; instrument/prove or do not enable |
| Time | Cancellation checks around awaits only | Absolute monotonic deadlines: enumeration 5 s, one device's complete operation 10 s, entire refresh including cleanup 30 s; remaining deadline on every I/O |
| Cleanup | Managed fake IDisposable only | Nonblocking local release for finalizers; bounded explicit StopSession/TLS close before release |
| Refresh | App coalesces pending work; one active discovery per instance | One active native refresh across backend instances, at most one pending UI refresh, no auto polling/retry, bounded stop before module disposal |

Actual failures precede any potential managed checks:

- property_list_service.c 194–225 accepts a partial nonzero four-byte header,
  allocates the advertised uint32 pktlen (212), then reads without an overall
  deadline. A success with zero further bytes can make no progress indefinitely.
- libusbmuxd.c 365–375 subtracts sizeof(header) without a minimum-length check,
  allocates payload_size and repeatedly receives with per-call timeouts; zero-byte
  progress can loop. XML parsing and record duplication precede managed limits.
- ListDevices' node count and legacy Listen event collection have no Cardryft cap;
  enumeration alloc/realloc and multiplication happen before the returned count.
- ReadPairRecord duplicates native data using a uint64 length, then casts to
  uint32 (1703–1705); no application bound is checked first.
- TLS handshake WANT_READ repeats without an absolute deadline (idevice.c
  1343–1354). Session free sends/receives StopSession and TLS shutdown performs
  peer I/O. `Task.Run`, cancellation, or abandoning an await cannot terminate C
  work or justify releasing a live module.
- Constructor allocation checks, response/tag matching and malformed success
  cases also need repair. libusbmuxd.c 512 logs a tag mismatch and proceeds;
  production must instead reject response/request mismatches.

The 4D OpenSSL configuration blocks autoload configuration, DSO/providers/engines
and dynamic compression. That useful closure result does not bound TLS memory or
authenticate peers. Parser-debug environment code is compiled out in plist's
generated config (`DEBUG` undefined); libimobiledevice debug code is present but
its static level defaults to zero and its setter is not in the nine exports.
Never expose debug setters or raw plist/credential logging. usbmux has level-zero
stderr errors; privacy review of all enabled diagnostics remains required.

## C ABI table and ownership

All declarations below were compared directly with pinned headers and source,
not .NET bindings. `LIBIMOBILEDEVICE_API` / `PLIST_API` export decorations are
omitted from declarations for readability. No P/Invoke is implemented. These are
**candidate signatures**, not an approved executable interop surface.

Windows AMD64 uses the unified x64 C calling convention; a future declaration
must explicitly use Cdecl, exact symbol spelling, no SetLastError inference and
no implicit ANSI marshalling. C enum/int are signed 32-bit for this toolchain;
uint32_t is 32-bit, uint64_t/size_t and pointers are 64-bit. `int *ssl_enabled`
is an out int, not a managed bool. Strings are NUL-terminated UTF-8 unless a
byte count is explicit. Input pointers are borrowed for the call. Output owners
must be initialized to NULL and checked even on success; several C failure paths
do not initialize outputs. Return error codes themselves own no memory.

`idevice_info_t` is a pointer to a struct, not the struct value. Enumeration is
an array of those pointers plus a NULL terminator. Derived x64 struct layout is
udid pointer at 0, int32 conn_type at 8, padding 12–15, conn_data pointer at 16,
size 24. Connection values are USBMUXD=1, NETWORK=2; open USB bit is **2**, not 1.
No compiled sizeof/offsetof or synthetic call probe was run; this layout still
requires a hardware-free native ABI fixture. Device/lockdown/plist are opaque.

| DLL / symbol | Exact C declaration | Ownership, nullability, encoding, release / errors |
| --- | --- | --- |
| libimobiledevice-1.0.dll / idevice_get_device_list_extended | `idevice_error_t idevice_get_device_list_extended(idevice_info_t **devices, int *count);` | Non-null output slots; owns pointer array and entries including UTF-8 UDID / conn_data; use extended_free on complete output, never free elements separately; int32 count; I errors; native allocation occurs before count check |
| same / idevice_device_list_extended_free | `idevice_error_t idevice_device_list_extended_free(idevice_info_t *devices);` | Consumes whole enumeration; NULL accepted; returns success; walks until NULL, not count; duplicate free unsafe |
| same / idevice_new_with_options | `idevice_error_t idevice_new_with_options(idevice_t *device, const char *udid, enum idevice_options options);` | Non-null initialized output; caller owns successful opaque device, idevice_free; UTF-8 UDID C permits NULL but Cardryft must forbid it; int32 options fixed 2; I errors |
| same / idevice_free | `idevice_error_t idevice_free(idevice_t device);` | Consumes non-null device heap state, no session free; NULL invalid; success/invalid argument; device must outlive lockdown's borrowed device pointer |
| same / lockdownd_client_new | `lockdownd_error_t lockdownd_client_new(idevice_t device, lockdownd_client_t *client, const char *label);` | Non-null borrowed device/output, nullable UTF-8 label duplicated internally (fixed Cardryft label if used); owns client, client_free; L errors; construction has prohibited implicit read and unchecked allocation |
| same / lockdownd_client_free | `lockdownd_error_t lockdownd_client_free(lockdownd_client_t client);` | Consumes non-null client, may StopSession and do TLS I/O; L errors even after owner freed; no retry-free; unsafe as unbounded finalizer release |
| same / lockdownd_get_value | `lockdownd_error_t lockdownd_get_value(lockdownd_client_t client, const char *domain, const char *key, plist_t *value);` | Borrowed non-null client; nullable UTF-8 domain/key in C; Cardryft domain=NULL/key=four constants only; non-null initialized value slot; owned copied Value via plist_free; missing Value may leave NULL with success; L errors |
| same / lockdownd_start_session | `lockdownd_error_t lockdownd_start_session(lockdownd_client_t client, const char *host_id, char **session_id, int *ssl_enabled);` | Client/UTF-8 existing HostID required (C null guard defective); optional output slots; session string is strdup copy, client separately owns session; prefer NULL session_id output to avoid unmanaged free ambiguity; ssl_enabled int32; L errors; require SSL=1 plus authenticated session, not mere success |
| same / lockdownd_stop_session | `lockdownd_error_t lockdownd_stop_session(lockdownd_client_t client, const char *session_id);` | Non-null borrowed client/UTF-8 session ID; releases internal session after response, may TLS close; L errors; not local-only release or duplicate disposal mechanism |
| libplist-2.0.dll / plist_free | `void plist_free(plist_t plist);` | Consumes owned node and descendants; NULL tolerated by implementation; no errors; never free a borrowed child independently |
| same / plist_get_node_type | `plist_type plist_get_node_type(plist_t node);` | Borrowed node; NULL returns PLIST_NONE=-1; int32 enum; PLIST_STRING=3; require string for metadata, reject composite values |
| same / plist_get_string_ptr | `const char* plist_get_string_ptr(plist_t node, uint64_t* length);` | Borrowed immutable UTF-8 buffer valid only while node is alive; optional length pointer (Cardryft must provide initialized uint64); NULL/wrong type returns NULL; check length before any copy/decoding, keep owner alive; do not free buffer |

Error groups, exact header values:

- **I / idevice_error_t:** SUCCESS=0, INVALID_ARG=-1, UNKNOWN_ERROR=-2,
  NO_DEVICE=-3, NOT_ENOUGH_DATA=-4, CONNREFUSED=-5, SSL_ERROR=-6, TIMEOUT=-7.
- **L / lockdownd_error_t:** SUCCESS=0, INVALID_ARG=-1, INVALID_CONF=-2,
  PLIST_ERROR=-3, PAIRING_FAILED=-4, SSL_ERROR=-5, DICT_ERROR=-6,
  RECEIVE_TIMEOUT=-7, MUX_ERROR=-8, NO_RUNNING_SESSION=-9, INVALID_RESPONSE=-10,
  MISSING_KEY=-11, MISSING_VALUE=-12, GET_PROHIBITED=-13, SET_PROHIBITED=-14,
  REMOVE_PROHIBITED=-15, IMMUTABLE_VALUE=-16, PASSWORD_PROTECTED=-17,
  USER_DENIED_PAIRING=-18, PAIRING_DIALOG_RESPONSE_PENDING=-19,
  MISSING_HOST_ID=-20, INVALID_HOST_ID=-21, SESSION_ACTIVE=-22,
  SESSION_INACTIVE=-23, MISSING_SESSION_ID=-24, INVALID_SESSION_ID=-25,
  MISSING_SERVICE=-26, INVALID_SERVICE=-27, SERVICE_LIMIT=-28,
  MISSING_PAIR_RECORD=-29, SAVE_PAIR_RECORD_FAILED=-30, INVALID_PAIR_RECORD=-31,
  INVALID_ACTIVATION_RECORD=-32, MISSING_ACTIVATION_RECORD=-33,
  SERVICE_PROHIBITED=-34, ESCROW_LOCKED=-35,
  PAIRING_PROHIBITED_OVER_THIS_CONNECTION=-36, FMIP_PROTECTED=-37,
  MC_PROTECTED=-38, MC_CHALLENGE_REQUIRED=-39, UNKNOWN_ERROR=-256.

These must be explicitly translated, never cast to the unrelated managed
NativeDeviceError enum. Missing/invalid trust material maps to unavailable/
NotTrusted without repair, locked/prohibited access to Restricted, lost USB to
NoDevice, service failure to TransportUnavailable, malformed/unknown values to
generic failure with no metadata. Exact vendor error semantics still need offline
protocol fixtures and later permitted hardware evidence. Managed fake error tests
are not native error-code validation.

Additional authentication primitives already called **internally**, not approved
as new P/Invokes:

| DLL / declaration | Ownership / behavior |
| --- | --- |
| libusbmuxd-2.0.dll / `int usbmuxd_read_buid(char **buid);` | Non-null initialized out slot; allocated UTF-8 C string, negative errno-style errors / 0; internal caller uses C free; no record write |
| same / `int usbmuxd_read_pair_record(const char *record_id, char **record_data, uint32_t *record_size);` | Non-null UTF-8 ID/output slots; allocated binary blob, C free, negative error / 0; not bounded and may return success without useful data; no selected generic managed credential API |
| libplist-2.0.dll / `plist_err_t plist_from_memory(const char *plist_data, uint32_t length, plist_t *plist, plist_format_t *format);` | Borrowed bounded bytes, owning out node via plist_free; format optional; error 0/-1/-2/-3/-4/-5/-255; parser budget required |
| same / `plist_t plist_dict_get_item(plist_t node, const char *key);` | Borrowed node/UTF-8 key; nullable borrowed child, no separate free; credential lookup internal only |
| same / `void plist_get_string_val(plist_t node, char **val);` | Duplicates UTF-8 value before caller can check length; wrong type does nothing; initialize out NULL; use plist_mem_free for libplist allocations; excluded from proposed bounded metadata reads |
| same / `void plist_mem_free(void *ptr);` | Releases only matching libplist buffers, not nodes; not a documented free for usbmux malloc/lockdown strdup; no CoTaskMem/managed arbitrary free substitution |

Future ownership types must be internal SafeHandles for device/list/plist and an
owned session wrapper retaining device/module lifetime. A lockdown SafeHandle is
blocked until a bounded local-only release exists; explicit graceful close must
have a deadline, and the finalizer must not send protocol requests. Do not add an
empty/leaking SafeHandle or a fake-only release test and call this issue resolved.
Partial success, failure allocations, cancellation and double-dispose need native
synthetic fixtures before approval. No raw owning IntPtr may escape interop.

## Loader policy and promotion

Today's NativeLibraryLoader is only a path/architecture/environment preflight;
both absent and present directories return an unavailable diagnostic. Direct
calls to UnvalidatedNativeBackend also throw Unsupported. It does not parse a
production manifest, hash DLLs or invoke the OS loader. No production manifest
has been created from unsafe candidate hashes.

A future production loader must validate all gates before **any** native code,
including DLL initialization, runs. Required policy:

1. Only the absolute composed application root plus `native\win-x64`; reject
   UNC/device namespaces/network drives, traversal segments, ADS, reparse points
   and final resolved path escapes. No PATH/CWD/TEMP/Downloads/user-selected search.
2. Exact six filename, byte-size and SHA-256 pins from an approved build, AMD64
   PE32+, bounded PE parsing, no unexpected files/DLLs, and exact recursive normal/
   delay imports and required symbols. System DLL names resolve only through
   approved Windows system locations, never local copied Apple/system binaries.
3. Retain race-resistant verified file/directory ownership through loading; reject
   replacement between hash inspection and load. Use explicit Windows dependency
   search flags restricted to the reviewed directory and System32; check existing
   same-name modules rather than accepting process-global loader reuse. No global
   PATH or DLL-search mutation/fallback. Audit initialization and unloading, too.
4. Keep modules alive until all operations/handles have retired; every failure
   must leave backend unavailable. Build inspect-pe.ps1 is not a production parser.

The exact six staged names/hashes remain in 4D evidence and 4E recheck evidence;
no assets were copied into `native/win-x64` (directory absent). No Apple binaries
are selected. Prefer generated native binaries as a separately versioned release
asset with matching source/notices, rather than committed Git binaries. Existing
ignores exclude runtime DLLs. Neither release assets nor commits were created.

## LGPL/source/rebuild/replacement status

The [notices ledger](../../THIRD-PARTY-NOTICES.md) and source-package script were
reviewed. Four libraries select LGPL-3.0 through their LGPL-2.1-or-later permission,
with preserved file exceptions/notices; OpenSSL is Apache-2.0. GCC's runtime
exception and exact MinGW terms remain part of the native material. Cardryft MIT
LICENSE is unchanged. GNU device tools/daemon and Apple binaries are excluded.
Source material hashes are verified, not a claim of release compliance.

For a recipient with a fresh complete Cardryft source workspace on Windows x64,
these existing scripts rebuild the **unmodified candidate**, without hardware:

```powershell
.\native-build\acquire.ps1
.\native-build\prepare.ps1
.\native-build\build.ps1 -Label RecipientA
.\native-build\build.ps1 -Label RecipientB
.\native-build\audit.ps1 -Label RecipientA
.\native-build\audit.ps1 -Label RecipientB
.\native-build\compare.ps1 -BuildA RecipientA -BuildB RecipientB
.\native-build\package-source.ps1 -Label RecipientA
.\scripts\validate.ps1
```

These commands were **not rerun in 4E**. Acquisition is the only native network
step; supply the exact locked archives under `.local/native-build/downloads` for
subsequent offline steps. Reuse an already verified prepared toolchain and fresh
labels when appropriate. Scripts refuse overwriting old runs/material. The staged
source package includes original library archives, all patches/recipes/locks,
compiler-runtime corresponding sources and complete license notices; a release
must additionally supply complete corresponding application source and final
installation/relink instructions with equivalent source access. The E package's
frozen notices describe its 4D build, not a newly approved 4E release.

LGPL-3.0 section 4(d)(0) is the planned source/recombination route, rather than a
claim that official exact-hash loading satisfies 4(d)(1) replacement automatically.
In a future recipient-owned source copy: edit compatible library source/patches,
update the local patch hash/source lock, rebuild/audit with fresh labels, regenerate
the modified application's pins, and build/install that modified application with
the replacement DLLs. **The last pin-generation/relink/install commands do not
exist yet.** No enabled backend/production manifest exists to exercise them. Thus
the practical replacement route is incomplete and release remains blocked; do
not add an unsigned-DLL override to official builds as a substitute.

Applicable full texts are [LGPL-3.0](../../native-build/licenses/LGPL-3.0.txt),
[GPL-3.0](../../native-build/licenses/GPL-3.0.txt),
[GCC exception](../../native-build/licenses/GCC-Runtime-Library-Exception-3.1.txt)
and [Apache-2.0](https://www.apache.org/licenses/LICENSE-2.0). Apache section 4
requires retaining the license, change notices and applicable attributions.
Release counsel/reviewer must assess chosen version compatibility, GCC exception
eligibility, combined-work source/installation obligations and the tested modified
application route. This audit makes no legal guarantee. GNU web pages were
unavailable during this audit; repository-preserved full license texts were read.

## Tests, validation and next safe work

All existing tests remain. Added regressions exercise direct inactive-backend
rejection with missing/present directories, exactly four typed metadata fields
and no Pair/generic query contract, blank/128/129-character identifiers, and the
32-device acceptance boundary with every fake connection released once.
Existing tests cover unsafe roots, process architecture, endpoint overrides,
reparse/network paths, default fail-closed discovery, unknown/unconfirmed trust,
oversized managed strings/counts, generic errors, failure/cancellation disposal,
and refresh coalescing/stale results. No test contacts hardware or loads native DLLs.

| Requested native tests | Current status |
| --- | --- |
| Manifest/hash mismatch/wrong PE architecture/unexpected DLL | Static 4D build audit + 4E staged identity verification; production validator negative tests deferred |
| Unsafe path/transport override/loader fail closed | Existing managed preflight tests and new direct-backend regression |
| Four keys/no Pair/no arbitrary query/existing trust only | Managed contract/fake tests; constructor violates native key gate |
| Oversized strings/device counts | Managed synthetic checks only; pre-marshalling native bounds not validated |
| Oversized plist/frame/allocations/deadlines | Failed static gate; bounded native fixture tests deferred |
| Native numeric error mapping | Exact codes audited; managed fake categories tested; executable C-to-managed mapping deferred |
| SafeHandle release/duplicate disposal | No native SafeHandles implemented; tests deferred until bounded cleanup/ABI fixture exists |

Complete .NET validation uses `.\scripts\validate.ps1` (`dotnet restore`,
`dotnet build -c Release`, `dotnet test -c Release`) with repository-local state.
All three commands were executed successfully in 4E: **0 build warnings / 0 errors;
158 total / 158 passed / 0 failed / 0 skipped**. Nine regression cases were added
to the existing 149; no tests were removed, weakened or retried. The local log is
`.local/milestone4e-validation-20261008.log`; results are also in the 4E evidence.
Passing .NET validation cannot certify native safety. No new package, project
reference or production behavior is introduced; LICENSE is preserved.

Files changed for 4E (all uncommitted):

- `AGENTS.md`, `README.md`, `THIRD-PARTY-NOTICES.md`.
- `docs/architecture/overview.md`, `docs/security/threat-model.md`.
- `docs/research/native-dependency-manifest.md`, `docs/research/windows-ios-device-access.md`.
- `tests/Cardryft.Tests/DeviceDiscoveryTests.cs`, `tests/Cardryft.Tests/NativeLibraryLoaderTests.cs`.
- New: `docs/research/native-runtime-safety.md`, `native-build/evidence/milestone4e-results.json`.

Ignored generated material is the new validation log, the selected-source check
directory, and normal validation `.local`/bin/obj outputs. Existing native builds,
recipes, audit logs, source package and 4D evidence are preserved. No Git
configuration/history/commits/pushes are changed.

Next work must be a separately reviewed native hardening revision: remove
constructor implicit reads, define authenticated existing-trust-only session
construction without record repair, prove fixed USB endpoint and read-only record
behavior, enforce native pre-allocation/parser/deadline limits and non-I/O release,
then add hardware-free protocol/ABI/loader fixtures. Those changes necessarily
produce **new hashes**, requiring new locked patches, two clean-build comparison,
PE/dependency/license audit and source/replacement verification. They cannot be
silently substituted under the requirement to promote exact 4D hashes. Do not
connect a phone to settle these source-level blockers; hardware remains a later
explicitly authorized milestone after every gate passes.
