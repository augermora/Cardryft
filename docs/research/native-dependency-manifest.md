# Native dependency evidence and reproducible build pipeline

## Milestone 4D source build (2026-10-08)

The [implemented build pipeline](../../native-build/README.md) supersedes the 4C
proposal below. 4C's mixed-version candidate hashes are historical evidence, never
an allowlist for new outputs. No native code is loaded by Cardryft and no device is
enumerated/queried in this milestone. Artifacts remain under ignored
`.local/native-build/runs/<label>/runtime`; `native/win-x64` is untouched.

### Locked inputs and graph

The [source lock](../../native-build/sources-lock.json) includes all five exact
repositories, release tags/commits, measured archive hashes, license/build-system
identities and hashed project patches. Every acquired archive matched that lock.
These are the actual 4D runtime source pins (each archive was rehashed on resume):

| Source | Exact upstream commit | Release archive SHA-256 |
| --- | --- | --- |
| libimobiledevice 1.4.0 | `149f7623c672c1fa73122c7119a12bfc0012f2ac` | `23cc0077e221c7d991bd0eb02150a0d49199bcca1ddf059edccee9ffd914939d` |
| libplist 2.7.0 | `cf5897a71ea412ea2aeb1e2f6b5ea74d4fabfd8c` | `7ac42301e896b1ebe3c654634780c82baa7cb70df8554e683ff89f7c2643eb8b` |
| libimobiledevice-glue 1.3.2 | `aef2bf0f5bfe961ad83d224166462d87b1df2b00` | `6489a3411b874ecd81c87815d863603f518b264a976319725e0ed59935546774` |
| libusbmuxd 2.1.1 | `adf9c22b9010490e4b55eaeb14731991db1c172c` | `5546f1aba1c3d1812c2b47d976312d00547d1044b84b6a461323c621f396efce` |
| OpenSSL 3.6.5 | `c8bd5a57108599ac650bbae77fcabe3109dab2e8` | `a2157c2830efdec3788939b00c9b0638306d3f0bbb76dc4832ee503bb397df98` |

Repository/archive URLs, release tags, build systems, licenses and seven patch hashes
are in the source lock. The compiler/runtime source archive pins are in the tool lock.
Build order/runtime graph is OpenSSL 3.6.5, libplist C 2.7.0, glue 1.3.2,
libusbmuxd client 2.1.1 and libimobiledevice 1.4.0. No tatsu/curl prerequisite is
needed after removing the tools-only configure check. No C++/Python wrapper,
wireless pairing helper, app/content service, CLI utility, GNU usbmuxd, readline,
Apple component, external provider/engine or zlib runtime is selected.

The libimobiledevice patch keeps idevice/service/property_list_service/lockdown
and common debug/userpref sources. It excludes the other services and restricts
root exports to exactly:

```text
idevice_get_device_list_extended
idevice_device_list_extended_free
idevice_new_with_options
idevice_free
lockdownd_client_new
lockdownd_client_free
lockdownd_get_value
lockdownd_start_session
lockdownd_stop_session
```

These native primitives are not a public UI API. Generic options/key arguments
still require the existing managed USB/four-key boundary and reviewed interop.
Pair/Unpair/handshake/StartService/file/app/content entry points are not in this
export list. Windows internal `dllexport` annotations are removed by a separate
hashed patch, because they otherwise augment the `.def` and exposed 73 exports in
the exploratory probe. A fresh root-only probe verified exactly nine exports before
final builds. No protocol implementation changes are made by these build patches.
Glue/usbmux's `-lIphlpapi` spelling is patched to the pinned MinGW `-liphlpapi`;
both generated/template makefiles are covered so libtool produces shared libraries.

### Toolchain and exact commands

The [toolchain lock](../../native-build/toolchain-lock.json) records the official
MSYS2 base 2026-09-27 (builder commit `cab3ac77dd72651a5dcfe16cb327abc2fa529121`,
archive SHA-256 `ea2f31a0b6ade63914ce441ffb022f0f6aa96982bfefa2326460a26d5fb01322`)
and each of 49 exact overlay package archives/hashes/dependencies/licenses.
It uses GCC **16.2.0-4**, GNU binutils/ld **2.47-3**, UCRT64 MinGW CRT/headers
**14.0.0.r426.g4564ee4b5-1**, GNU make **4.4.1-3**, Perl **5.42.3-2**,
autoconf **2.73-1**, automake **1.18.1-1**, libtool **2.6.2-1** and UCRT pkgconf
**1~3.0.7-1**. Wrapper/transitive tool versions are also locked; they are not
application dependencies. MinGW revision is `4564ee4b5063097bf747af3a3f8270a28adff820`.
Matching GCC/CRT/header source packages and individual recipe/patch hashes are recorded.
The actual GNU ld version output is `2.47.20260726` from package `2.47-3`.

Native development commands are:

```powershell
.\native-build\acquire.ps1
.\native-build\prepare.ps1
.\native-build\build.ps1 -Label E
.\native-build\build.ps1 -Label F
.\native-build\audit.ps1 -Label E
.\native-build\audit.ps1 -Label F
.\native-build\compare.ps1 -BuildA E -BuildB F
.\native-build\package-source.ps1 -Label E
```

The exact shell configure/make/install/inspection commands and flags are in
`native-build/build.sh` and per-build `build.log`. Bootstrap archive extraction initially
failed on Windows symlinks, so preparation uses MSYS archive semantics and repository-local
regular copies. MSYS could not initialize in Codex's restricted process sandbox;
the controlled runner executed outside that sandbox with ordinary user privileges.
An initial exploratory build was stopped, not counted as reproducibility evidence.
On resumption, all acquired archive hashes and the prepared toolchain lock were
verified and reused; no old directory/log was deleted, reset, cleaned or overwritten.
The prior partial runs are retained and excluded from final reproducibility evidence:

| Retained attempt | Verified result / reason it cannot qualify |
| --- | --- |
| A-initial | Interrupted exploratory recipe; no complete audited runtime |
| A/B | Older CryptoAPI RNG OpenSSL builds completed; full builds stopped at libtool's invalid inherited stdin descriptor |
| C/D | CNG object compilation completed; full link failed because `-lbcrypt` preceded the provider archive |
| A library probe | Six DLLs compiled after fixing MinGW library spelling, then the probe's final status message failed on an unset label; static inspection also found 73 root exports |
| exports-resume | New isolated root-only probe passed with nine exports against A's read-only staged dependencies; no crypto rebuild and not a full clean-build claim |

The runner now gives the child valid closed stdin and `build.sh` redirects from
`/dev/null`. BCrypt is a final Configure library argument, not an early linker flag.
These specific fixes and the pinned export/case patches require fresh final E/F
builds; no failed attempt is accepted by rerunning unchanged or suppressing errors.
Each clean build freezes its recipe/locks/hashes and refuses existing directories.

Builds use fixed locale/timezone/epoch, explicit dependency/pkg-config paths,
compiler file/debug/macro prefix maps, zero PE timestamps, no build IDs and static
libgcc. OpenSSL's logical prefix is fixed; installation uses per-build DESTDIR.
The shared-only OpenSSL make targets skip unused static libcrypto/libssl work;
provider archives required by the shared libraries are still compiled and linked.
The informational build-text patch prevents embedded remapping flags from revealing
different build directories. Real compiler arguments remain in logs. No system
installation, live package resolution, source clone, driver/tool execution or device
operation occurs. Parent environment variables are unchanged; child state stays in `.local`.

### Measured clean-build results and recursive closure

Clean E and F both exited **0**. Their frozen `build-inputs.json` SHA-256 is
`ca4e78ff4aa2b0022be250328a53fb37a5df9b0011d6b3eb48376d01c2558364`.
Both PE audits exited 0; the comparison checked actual bytes and rehashed audited
files, then exited 0 with **6/6 byte-for-byte identical**. No differences need
normalization after the build. This proves repetition in these two separate clean
directories with this same locked toolchain/host, not independent compiler rebuilding
or reproducibility on an untested Windows installation.

| DLL | Bytes | Named exports | SHA-256 in both E and F |
| --- | ---: | ---: | --- |
| libcrypto-3-x64.dll | 9,031,118 | 5,696 | `e739523e963c313ad5f6c07275fe58ae5c8f2e3f8c2871096990b2cb14dfa2fd` |
| libssl-3-x64.dll | 1,186,777 | 594 | `9cf8cd67df7f3eddff3d145cb4513f9e9745d35974d147a1c8b3b0ae93cbef45` |
| libplist-2.0.dll | 181,118 | 105 | `bd3a992957bc6758b59f1da84bc831a2e455b421f39e8f85e8140b07cff60537` |
| libimobiledevice-glue-1.0.dll | 151,911 | 110 | `f1ee158d22ac3938536a9b50141b5acbd1f1bfb7935368beea070107e1569386` |
| libusbmuxd-2.0.dll | 66,323 | 21 | `9e2edc02b3a243c5c03d06be3ee42210fc07b2ce8270f5a72b561dd599df0052` |
| libimobiledevice-1.0.dll | 132,876 | 9 | `8343a8c164d574814f8d26264e2854288909bf7e392c17db0f52e0c59a933b29` |

Every DLL is **AMD64 (`0x8664`), PE32+ (`0x020b`)**, with zero COFF timestamp,
zero delay imports and zero forwarded exports. No MSYS, libgcc, winpthread, zlib,
curl, tatsu or other non-Windows DLL is required outside these six files.

| Importing DLL | Exact non-Windows imports | Other Windows imports, plus per-file UCRT API sets |
| --- | --- | --- |
| libimobiledevice-1.0.dll | crypto, ssl, plist, glue, usbmux DLLs listed above | KERNEL32, ole32, SHELL32 |
| libssl-3-x64.dll | libcrypto-3-x64.dll | KERNEL32 |
| libusbmuxd-2.0.dll | libimobiledevice-glue-1.0.dll, libplist-2.0.dll | KERNEL32, WS2_32 |
| libimobiledevice-glue-1.0.dll | libplist-2.0.dll | IPHLPAPI, KERNEL32, WS2_32 |
| libplist-2.0.dll | None | KERNEL32 |
| libcrypto-3-x64.dll | None | ADVAPI32, bcrypt, CRYPT32, KERNEL32, USER32 |

The full per-file UCRT import names and every named export, exact source/patch/license
mapping, build input hash and comparison result are in
[milestone4d-results.json](../../native-build/evidence/milestone4d-results.json).
This tracked text evidence is justified for review; it is explicitly **not a loader
manifest** and `runtimeApproved` is false. DLLs, toolchain, source packages and raw
objdump/config/build logs remain ignored. Independent GNU objdump architecture/import
inspection agreed with the byte parser; crypto imports `BCryptGenRandom` and imports
neither `CryptAcquireContext` nor `CryptGenRandom`.

Each native build reported **6 compiler warning lines / 0 errors**, unchanged and
unsuppressed: OpenSSL discarded `const`; plist transposed `calloc` arguments; glue
two nonnull comparisons and an unused variable; usbmux a shadowed `stpncpy`.
These upstream diagnostics are retained for future native security/compatibility review.

`package-source.ps1 -Label E` exited 0 and staged eight exact source archives (five
libraries plus GCC/CRT/headers), frozen recipes/patches/locks, upstream notices/full
license texts and a material SHA-256 inventory in
`.local/native-build/runs/E/release-material`. It includes no runtime DLL or Apple
binary and is not a published or fully approved release package. All 62 packaged
source/notice/recipe files were rehashed against the generated material inventory;
the package contains zero `.dll`, `.exe` or `.pdb` files.

### Dynamic-loading review

OpenSSL is configured `shared no-autoload-config no-dso no-module no-engine no-comp
no-zlib no-legacy no-sock no-apps no-tests`. Its default provider is built in.
`ssl/ssl_init.c` and provider initialization guard autoload with
`OPENSSL_NO_AUTOLOAD_CONFIG`; the selected libimobiledevice initialization supplies
zero config flags. Thus OPENSSL_CONF/default openssl.cnf/OPENSSL_CONF_INCLUDE do not drive
automatic configuration. Explicit config APIs remain in upstream crypto and must
never be bound or invoked by Cardryft.

`no-dso` chooses the non-loading DSO backend. OPENSSL_MODULES/default module paths
cannot make that backend load a provider DLL; unknown providers fail instead of
searching for arbitrary code. `no-module/no-engine/no-legacy` excludes engine and
legacy module artifacts. No dynamic/static zlib/compression path is selected.
No non-default external provider is needed by the compiled pairing/TLS code;
actual existing-session compatibility is untested. Socket BIO removal excludes
OpenSSL's direct socket networking; libimobiledevice's custom BIO remains compiled.
The other recorded OpenSSL patch enables `BCryptGenRandom` with
`BCRYPT_USE_SYSTEM_PREFERRED_RNG` for MinGW on Windows 10+, rather than allowing
registry-selected legacy CryptoAPI/Intel CSP providers. OS entropy remains selected
with `--with-rand-seed=os`; there is no deterministic/test RNG or TLS policy downgrade.

This is a constrained call path, not an assertion that every OpenSSL environment
variable is inert. CPU-capability/randomness/diagnostic controls and explicit certificate
store/file APIs remain general-purpose upstream behavior. The future caller must
use explicit trusted-session inputs and reject unsafe transport/config overrides.
`USBMUXD_SOCKET_ADDRESS` remains upstream behavior that can redirect transport to a
remote endpoint. Current Cardryft preflight rejects any override and cannot load
the runtime; future interop must preserve a race-resistant loopback-only policy.
Cryptographic randomness remains intentional; reproducibility concerns binary bytes.
Compiler/CRT OS symbol resolution is separate from provider loading; Windows system
imports must never be gathered or redistributed as a supposed portable DLL closure.

### Licensing and remaining release/interop gates

[THIRD-PARTY-NOTICES](../../THIRD-PARTY-NOTICES.md) reconciles selected compiled
library sources/libcnary, plist's MIT exceptions, glue's LibTomCrypt notices,
Apache-2.0 OpenSSL, LGPL-3.0 selection through "or later", GCC's runtime exception,
and the exact MinGW CRT/header notices. Full license texts, exact corresponding
source packages, recipes and patches are prepared by `package-source.ps1`.
Cardryft's MIT LICENSE is unchanged. Toolchain binaries and Apple components are
not application redistributables.

No production loader manifest is generated: an end-to-end modified-library/app
replacement route cannot yet be exercised because the application has no enabled
interop/pin generator. Native frame allocation/total deadlines and ABI cleanup
remain unreviewed. Upstream `SSL_CTX_set_security_level(..., 0)` and
`SSL_set_verify(..., 0, ...)` must be explicitly justified/authenticated before
existing-trust device use; the recipe adds no security downgrade and does not claim
this TLS path is approved. Non-handshake construction also reads DeviceClass and
ProductVersion implicitly. Root export reduction alone does not solve these gates.

Until those gates pass, artifacts are audit candidates, **not ready for production
promotion or hardware use**. Device Refresh still fails closed. No new production
data structure, managed dependency or test is introduced.

### Cardryft validation

`.\scripts\validate.ps1` executed `dotnet restore`, `dotnet build -c Release`,
and `dotnet test -c Release` with isolated repository-local state. All succeeded:
**0 build warnings / 0 errors; 149 total / 149 passed / 0 failed / 0 skipped**.
No tests were weakened, removed or retried. No phone/native-runtime/device-tool
execution is part of those tests.
The fresh resume validation log is `.local/native-build/validation-resume-20261008.log`.
Production sources, tests, project references, managed packages and LICENSE remain
unchanged. No new production data structures were introduced, so no speculative
loader tests were added.

### Complete uncommitted file inventory

Changed existing files: `.gitattributes`, `.gitignore`, `AGENTS.md`, `README.md`,
`THIRD-PARTY-NOTICES.md`, `docs/architecture/overview.md`,
`docs/research/native-dependency-manifest.md`, `docs/research/windows-ios-device-access.md`,
`docs/security/threat-model.md`.

New files under `native-build/`:

- `README.md`, `acquire.ps1`, `prepare.ps1`, `prepare.sh`, `invoke-msys.ps1`,
  `build.ps1`, `build.sh`, `inspect-pe.ps1`, `audit.ps1`, `compare.ps1`, `package-source.ps1`.
- `sources-lock.json`, `toolchain-lock.json`, `toolchain-source-evidence.json`.
- `patches/cardryft.exports`, `patches/libimobiledevice-minimal.patch`,
  `patches/libimobiledevice-windows-exports.patch`, `patches/glue-windows-library-case.patch`,
  `patches/libusbmuxd-windows-library-case.patch`, `patches/openssl-build-info.patch`,
  `patches/openssl-system-rng.patch`.
- `licenses/Apache-2.0.txt`, `licenses/LGPL-3.0.txt`, `licenses/GPL-3.0.txt`,
  `licenses/GCC-Runtime-Library-Exception-3.1.txt`, `licenses/MinGW-w64-runtime.txt`.
- `evidence/milestone4d-results.json`.

Earlier local evidence/runs were preserved. New ignored work is limited to final E/F
recipes/builds/audits/comparison/source material, the isolated `exports-resume` probe,
its shell recipe/state and the fresh .NET validation log/cache/build output. No Git
configuration, history, commits, pushes or publication changed.

## Historical Milestone 4C audit and proposal

Audit date: 2026-10-07. **Status: candidate audit only; no approved Cardryft runtime.**

Milestone 4C stops before native loading/P/Invoke. An exact static PE closure was
measured for a traceable MSYS2 candidate, but its full dynamic-load closure and
distribution compliance are not established. Do not treat this document as an
application allowlist or copy these artifacts into `native/win-x64`.

## Artifact selection evidence

- The inspected [upstream 1.4.0 release assets](https://github.com/libimobiledevice/libimobiledevice/releases/expanded_assets/1.4.0)
  contain a source tarball and generated source archives, not Windows DLLs.
- The [jrjr v20261004-74585f8 build recipe](https://github.com/jrjr/libimobiledevice-windows/blob/74585f8/.github/workflows/build.yml)
  clones unpinned upstream branches, updates its toolchain and gathers a broad suite.
  Its release tag identifies the builder, not each upstream library revision.
  No suite archive was downloaded or adopted.
- The archived [libimobiledevice-win32 build project](https://github.com/libimobiledevice-win32/libimobiledevice-vs)
  is not the basis for this runtime.
- MSYS2 publishes package hashes, matching source recipes and build-environment
  records. Five specific UCRT64 packages were downloaded as inert audit data from
  `https://mirror.msys2.org/mingw/ucrt64/`; every archive's SHA-256 matched its
  project-owned package page. Only six named DLLs and provenance metadata were
  extracted, into ignored `.local/milestone4c-audit/quarantine*`. No installation,
  DLL loading, native initialization, device enumeration or executable extraction
  was performed. Archives also contain tools; those are not runtime selections.

Windows' existing `tar.exe` inspected named archive entries. A repository-local
PowerShell byte reader inspected PE headers/imports/exports without executing DLLs;
.NET PEReader independently confirmed AMD64/PE32+ and empty delay-import directories.
Detached package signatures and independent binary rebuilds were **not** verified.
HTTPS/published hashes and matching recipes establish useful traceability, not proof
that every binary was built from that source or that its code is safe.

## Measured candidate DLLs

Versions below are package/source identities, not inferred from the DLL filename.
The root is **patched 1.3.0**, not the proposed upstream 1.4.0 build. All six files
have COFF Machine `0x8664`, PE32+ `0x020b`, and no delay imports.

| Exact DLL | Source identity / upstream | Relationship / purpose | License evidence |
| --- | --- | --- | --- |
| `libimobiledevice-1.0.dll` | [libimobiledevice 1.3.0](https://github.com/libimobiledevice/libimobiledevice/tree/15f8652126664e3a4b980e5d1c039b9053ce8566), MSYS2 `1.3.0-17`, seven recipe patches | Candidate entry library for USB targets, lockdown and metadata | LGPL-2.1-or-later; audit compiled auxiliary/runtime code |
| `libusbmuxd-2.0.dll` | [libusbmuxd 2.1.1](https://github.com/libimobiledevice/libusbmuxd/tree/adf9c22b9010490e4b55eaeb14731991db1c172c), package `2.1.1-1` | Direct root dependency; client IPC to Apple USB transport | LGPL-2.1-or-later; separate tools are GPL |
| `libplist-2.0.dll` | [libplist 2.7.0](https://github.com/libimobiledevice/libplist/tree/cf5897a71ea412ea2aeb1e2f6b5ea74d4fabfd8c), package `2.7.0-4`, recipe patch | Direct root dependency and transitive through usbmux/glue; plist values and frees | Library source headers LGPL-2.1-or-later; package declares GPL AND LGPL, requiring file-level reconciliation |
| `libimobiledevice-glue-1.0.dll` | [glue 1.3.2](https://github.com/libimobiledevice/libimobiledevice-glue/tree/aef2bf0f5bfe961ad83d224166462d87b1df2b00), package `1.3.2-1` | Transitive through libusbmuxd; socket/thread helpers | LGPL-2.1-or-later |
| `libssl-3-x64.dll` | [OpenSSL 3.6.5](https://github.com/openssl/openssl/tree/c8bd5a57108599ac650bbae77fcabe3109dab2e8), package `3.6.5-1`, recipe patches | Direct root dependency; TLS for an existing trusted session | Apache-2.0; retained upstream notices and exact package changes |
| `libcrypto-3-x64.dll` | Same OpenSSL source/package | Direct root dependency and transitive through libssl; cryptography | Apache-2.0 plus embedded MSYS2 CC0 path helper; audit compiler code |

Measured **DLL** SHA-256 values (distinct from archive/source hashes):

```text
libimobiledevice-1.0.dll       0768d5491bd5e6a1fb7707d1c3524e8eacb24f9f5377cdc1c593e66e38bf78cf
libusbmuxd-2.0.dll            6f7d37d888ff3144fc09e23f90702324e9a96685352b8f559679cdfb4c5d69f0
libplist-2.0.dll              b4fbf69fa5b0c585dab5a466ebda760e4cc5881b1c593585e75d9dafadd5dc7c
libimobiledevice-glue-1.0.dll  e687690485f1f8749c62000337effa6f04baf7f9a8ce56b74d7181691e291de1
libssl-3-x64.dll              8b71cd694f8edfbddc76674111278db6bcdcde0dcd63b99238420193848e6dbf
libcrypto-3-x64.dll           97308feea369b305e562c0098638a1b02a06f52ee9fcc4b8cf3da362ff563cf4
```

The root exports the inspected discovery/free/new-with-options functions and
`lockdownd_client_new`, `lockdownd_client_free`, `lockdownd_get_value`,
`lockdownd_start_session`. Presence is a static observation, not an ABI, ownership,
trust or compatibility test. Forbidden functions are also exported by the broad
upstream library; Cardryft must never bind or expose them.

## Recursive PE import evidence

The six DLLs close their **static** non-system imports:

```text
libimobiledevice-1.0 -> libusbmuxd-2.0, libplist-2.0, libssl-3-x64, libcrypto-3-x64
libusbmuxd-2.0      -> libimobiledevice-glue-1.0, libplist-2.0
libimobiledevice-glue-1.0 -> libplist-2.0
libssl-3-x64        -> libcrypto-3-x64
libplist-2.0 / libcrypto-3-x64 -> Windows system imports only
```

There is no measured PE import of libtatsu, curl, Python, libusb, zlib,
libgcc_s, libstdc++ or libwinpthread. This does not exclude statically incorporated
compiler code or later dynamic loads. Windows imports are system prerequisites,
not files to collect from this workstation or redistribute. The complete measured
per-file import tables follow.

### libimobiledevice-1.0.dll

```text
libcrypto-3-x64.dll
IPHLPAPI.DLL
KERNEL32.dll
api-ms-win-crt-convert-l1-1-0.dll
api-ms-win-crt-filesystem-l1-1-0.dll
api-ms-win-crt-heap-l1-1-0.dll
api-ms-win-crt-locale-l1-1-0.dll
api-ms-win-crt-private-l1-1-0.dll
api-ms-win-crt-runtime-l1-1-0.dll
api-ms-win-crt-stdio-l1-1-0.dll
api-ms-win-crt-string-l1-1-0.dll
api-ms-win-crt-time-l1-1-0.dll
api-ms-win-crt-utility-l1-1-0.dll
ole32.dll
libplist-2.0.dll
SHELL32.dll
libssl-3-x64.dll
libusbmuxd-2.0.dll
WS2_32.dll
```

### libusbmuxd-2.0.dll

```text
libimobiledevice-glue-1.0.dll
KERNEL32.dll
api-ms-win-crt-convert-l1-1-0.dll
api-ms-win-crt-environment-l1-1-0.dll
api-ms-win-crt-filesystem-l1-1-0.dll
api-ms-win-crt-heap-l1-1-0.dll
api-ms-win-crt-locale-l1-1-0.dll
api-ms-win-crt-private-l1-1-0.dll
api-ms-win-crt-runtime-l1-1-0.dll
api-ms-win-crt-stdio-l1-1-0.dll
api-ms-win-crt-string-l1-1-0.dll
api-ms-win-crt-utility-l1-1-0.dll
libplist-2.0.dll
WS2_32.dll
```

### libplist-2.0.dll

```text
KERNEL32.dll
api-ms-win-crt-convert-l1-1-0.dll
api-ms-win-crt-filesystem-l1-1-0.dll
api-ms-win-crt-heap-l1-1-0.dll
api-ms-win-crt-locale-l1-1-0.dll
api-ms-win-crt-math-l1-1-0.dll
api-ms-win-crt-private-l1-1-0.dll
api-ms-win-crt-runtime-l1-1-0.dll
api-ms-win-crt-stdio-l1-1-0.dll
api-ms-win-crt-string-l1-1-0.dll
api-ms-win-crt-time-l1-1-0.dll
api-ms-win-crt-utility-l1-1-0.dll
```

### libimobiledevice-glue-1.0.dll

```text
IPHLPAPI.DLL
KERNEL32.dll
api-ms-win-crt-convert-l1-1-0.dll
api-ms-win-crt-environment-l1-1-0.dll
api-ms-win-crt-filesystem-l1-1-0.dll
api-ms-win-crt-heap-l1-1-0.dll
api-ms-win-crt-locale-l1-1-0.dll
api-ms-win-crt-private-l1-1-0.dll
api-ms-win-crt-runtime-l1-1-0.dll
api-ms-win-crt-stdio-l1-1-0.dll
api-ms-win-crt-string-l1-1-0.dll
api-ms-win-crt-time-l1-1-0.dll
api-ms-win-crt-utility-l1-1-0.dll
libplist-2.0.dll
WS2_32.dll
```

### libssl-3-x64.dll

```text
libcrypto-3-x64.dll
KERNEL32.dll
api-ms-win-crt-convert-l1-1-0.dll
api-ms-win-crt-environment-l1-1-0.dll
api-ms-win-crt-filesystem-l1-1-0.dll
api-ms-win-crt-heap-l1-1-0.dll
api-ms-win-crt-private-l1-1-0.dll
api-ms-win-crt-runtime-l1-1-0.dll
api-ms-win-crt-stdio-l1-1-0.dll
api-ms-win-crt-string-l1-1-0.dll
api-ms-win-crt-time-l1-1-0.dll
api-ms-win-crt-utility-l1-1-0.dll
WS2_32.dll
```

### libcrypto-3-x64.dll

```text
ADVAPI32.dll
CRYPT32.dll
KERNEL32.dll
api-ms-win-crt-convert-l1-1-0.dll
api-ms-win-crt-environment-l1-1-0.dll
api-ms-win-crt-filesystem-l1-1-0.dll
api-ms-win-crt-heap-l1-1-0.dll
api-ms-win-crt-locale-l1-1-0.dll
api-ms-win-crt-private-l1-1-0.dll
api-ms-win-crt-runtime-l1-1-0.dll
api-ms-win-crt-stdio-l1-1-0.dll
api-ms-win-crt-string-l1-1-0.dll
api-ms-win-crt-time-l1-1-0.dll
api-ms-win-crt-utility-l1-1-0.dll
USER32.dll
WS2_32.dll
```


## Package and corresponding-source integrity

Binary archives use prefix `mingw-w64-ucrt-x86_64-` and suffix
`-any.pkg.tar.zst`. The following hashes were locally calculated and matched the
linked MSYS2 publication:

| Package/version | Binary archive SHA-256 |
| --- | --- |
| [libimobiledevice-1.3.0-17](https://packages.msys2.org/packages/mingw-w64-ucrt-x86_64-libimobiledevice) | `2f8ede1528372fa0ec7d3ec9f248b22c43e3bb2f573025c9086781413d12a1d1` |
| [libusbmuxd-2.1.1-1](https://packages.msys2.org/packages/mingw-w64-ucrt-x86_64-libusbmuxd) | `27c86acc00eda349947fa0fde05cfca805ab1aee6da4cc4e4a6486aedf054f1c` |
| [libplist-2.7.0-4](https://packages.msys2.org/packages/mingw-w64-ucrt-x86_64-libplist) | `665607c34dbf923ebb820d78b376db59de4d2d3874a45d70f24f51eb1d64f127` |
| [libimobiledevice-glue-1.3.2-1](https://packages.msys2.org/packages/mingw-w64-ucrt-x86_64-libimobiledevice-glue) | `d089c718ab846c5bd1f41fb2788a976fdbda13c9f09128a670eca578da0bc34f` |
| [openssl-3.6.5-1](https://packages.msys2.org/packages/mingw-w64-ucrt-x86_64-openssl) | `773e021ebad83f14b5c103db467411e8ff064663e1c7e7f730e2be72f4117064` |

Corresponding source-only archives were acquired from
`https://mirror.msys2.org/mingw/sources/mingw-w64-<name>-<version>.src.tar.zst`.
Each extracted PKGBUILD hash matched the binary's `.BUILDINFO` exactly. Source-only
archive hashes below are measured evidence, not independently signed attestations.

| Name/version | Source-only archive SHA-256 | PKGBUILD SHA-256 |
| --- | --- | --- |
| libimobiledevice-1.3.0-17 | `ae03257e0b03ded7399b28cb9e794c5fda67c622a51f5a928fc23a1e5a6e9738` | `821defad111e36d4d2ca2a0141a73a354c37c1f6a9fdbb233535cf8790499c12` |
| libusbmuxd-2.1.1-1 | `d5d11d66e65e3926892b9f1301b8c75a4472f68184f86b21874026bcd4c28797` | `822bf6fafaa12f6a5eede9ce7a52980c9e531ae5d9d9a202ffcff18a24104b61` |
| libplist-2.7.0-4 | `0170254ac2a3e4fa0c88ec974a4e6d6a1e1b91d3da14d91b9b147a336c09a015` | `18acfabb481b3d6daffafa08856e0f5bf8433e8234036fde08dbc52657b8fd3a` |
| libimobiledevice-glue-1.3.2-1 | `ee156ff49a69840c1e919d50de5d06d935765882bd24fe74ff496f535f0fcdc3` | `5b147d7e438d61b5fd23ba5963ced35df1e83d2e6a20af0357bb60eec136a76a` |
| openssl-3.6.5-1 | `997157ec74a6a722d7c8848605b1389a31a103d4b9f2aa6a40e179373a139cd1` | `88548a50bbff0074ab4071483139ff81e81acb39ee2811a512f9f0a5a11388e6` |

Build records identify different GCC/UCRT environments: root GCC 15.2.0-11,
plist 15.2.0-9, usbmux/glue 15.1.0-5, OpenSSL 16.2.0-4. Root/plist use MinGW CRT
13.0.0.r453.gfd36ef357; usbmux/glue use 13.0.0.r21.gf5469ff36; OpenSSL uses
14.0.0.r426.g4564ee4b5. These are build provenance, not extra runtime DLL requirements.
Full `.BUILDINFO` records are retained under the ignored audit directory.
Notably root was built with OpenSSL 3.6.1-3 and usbmux/glue with plist 2.6.0-3;
the inspected candidate assembles newer same-named ABI dependencies. Compatibility
has not been demonstrated by compiling or executing that combination.

## Why the gate remains closed

1. The exact OpenSSL recipe enables `zlib-dynamic` and engines and contains a
   relocation patch for configuration/engine/provider directories. The archive
   includes `legacy.dll`, `capi.dll`, `loader_attic.dll`, `padlock.dll` and configuration.
   These were listed, not extracted/hashed/selected. Upstream
   [dynamic zlib code](https://github.com/openssl/openssl/blob/openssl-3.6.5/crypto/comp/c_zlib.c)
   loads `ZLIB1` on Windows. Its absence from PE imports is not proof it cannot load.
   [OpenSSL environment controls](https://docs.openssl.org/3.6/man7/openssl-env/)
   can select configuration, providers and engines. A full controlled runtime closure
   therefore remains unresolved; an absolute top-level DLL path alone is insufficient.
2. Package signatures/rebuild equivalence and compiled-source/static-runtime license
   inventories remain incomplete. The plist recipe explicitly tolerates `make check`
   failure. That recipe is not adopted as Cardryft's validation pipeline.
3. The candidate is not the proposed single-toolchain upstream 1.4.0 build. No
   resulting hardened artifacts, hashes, import closure or corresponding-source
   distribution exist yet. The candidate hashes must not authorize hypothetical builds.

This is not a finding that MSYS2 is malicious or that the candidate is necessarily
ABI-incompatible. It is an incomplete Cardryft approval gate. Production behavior
and all existing fake tests remain unchanged. See the [compliance plan](../../THIRD-PARTY-NOTICES.md).

## Proposed reproducible local build

**Proposal only; not run and not advertised as a proven reproducible build.** Use
upstream source archives rather than the unpinned Windows suite or opaque binaries.
The [upstream Windows guidance](https://github.com/libimobiledevice/libplist/blob/2.7.0/README.md#building)
uses MSYS2; use one verified x64 UCRT64 toolchain snapshot throughout.

Pinned source inputs:

| Source | Full upstream commit | Release tarball SHA-256 |
| --- | --- | --- |
| libimobiledevice 1.4.0 | `149f7623c672c1fa73122c7119a12bfc0012f2ac` | `23cc0077e221c7d991bd0eb02150a0d49199bcca1ddf059edccee9ffd914939d` |
| libplist 2.7.0 | `cf5897a71ea412ea2aeb1e2f6b5ea74d4fabfd8c` | `7ac42301e896b1ebe3c654634780c82baa7cb70df8554e683ff89f7c2643eb8b` |
| libimobiledevice-glue 1.3.2 | `aef2bf0f5bfe961ad83d224166462d87b1df2b00` | `6489a3411b874ecd81c87815d863603f518b264a976319725e0ed59935546774` |
| libusbmuxd 2.1.1 | `adf9c22b9010490e4b55eaeb14731991db1c172c` | `5546f1aba1c3d1812c2b47d976312d00547d1044b84b6a461323c621f396efce` |
| libtatsu 1.0.5, build prerequisite only | `42329cb756682535c7c0f087987b78d1dd5b16c8` | `536fa228b14f156258e801a7f4d25a3a9dd91bb936bf6344e23171403c57e440` |
| OpenSSL 3.6.5 | `c8bd5a57108599ac650bbae77fcabe3109dab2e8` | `a2157c2830efdec3788939b00c9b0638306d3f0bbb76dc4832ee503bb397df98` |

Upstream release API metadata supplied commits and four libimobiledevice-project
asset digests; plist's upstream archive was downloaded and hashed, matching the
corresponding MSYS2 recipe. OpenSSL's source digest comes from that exact recipe.
Unacquired source tarball digests are published/recipe evidence, not local verification.
Before building, independently verify downloaded source hashes/signatures and every
patch. Review freshness/security advisories again at build time; these pins are a
research baseline, not a permanent safe-version promise.

1. Prepare a verified toolchain snapshot under `.local/native-build/toolchain`,
   without global installation/elevation or Git changes. Pin and hash GCC, binutils,
   MinGW headers/CRT, make/autotools/pkgconf/Perl and all build-only prerequisites,
   including curl for libtatsu if needed. A complete toolchain lock is still required;
   no floating `pacman -Syu`, `windows-latest` or unpinned Git clones. Keep caches,
   logs, work directories and temporary files under `.local/native-build`.
2. After acquisition, build offline using verified source tarballs, fixed work
   paths/locale/timezone and SOURCE_DATE_EPOCH, path remapping and controlled PE
   timestamps. Record all compiler/linker flags, generated config headers and logs.
   Source code, including configure/make scripts, must be reviewed before execution.
3. Build OpenSSL shared libraries with the pinned source's documented restrictions,
   then plist, glue, usbmux, build-only tatsu and finally libimobiledevice. Example
   configuration goals, to verify against configure output rather than silently ignore:

   ```sh
   # Recipe sketch, not executed here; prefix is repository-local staging.
   perl Configure mingw64 shared no-autoload-config no-dso no-module no-engine no-comp no-zlib --prefix="$prefix"
   ./configure --host=x86_64-w64-mingw32 --prefix="$prefix" --enable-shared --disable-static --without-cython
   # Additional libimobiledevice 1.4.0 flags:
   # --without-readline --disable-wireless-pairing --disable-debug
   # --with-openssl --without-gnutls --without-mbedtls
   ```

   [OpenSSL build options](https://github.com/openssl/openssl/blob/openssl-3.6.5/INSTALL.md)
   support removing configuration autoload/DSO/engine support. Verify no dynamic
   compression/provider path remains. Verify whether those restrictions preserve
   existing-session TLS; do not lower security settings globally to make it work.
   The [1.4.0 link recipe](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/src/Makefile.am)
   links glue directly and includes `-static-libgcc` on Windows. Its
   [configure](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/configure.ac)
   requires tatsu although the core link recipe does not name it. Measure the new
   closure; do not copy the six-file candidate list blindly or ship tatsu/curl by default.
4. Stage libraries/headers/pkgconfig for builds; final runtime staging selects only
   the measured DLL closure. Exclude Python/C++ plist wrappers, CLI tools, static LGPL
   libraries, GNU usbmuxd, pairing tools, Apple binaries, debug output and unused modules.
   Run library/offline fixture checks with failures fatal; never use `make check || true`.
   These future checks must not enumerate or query real USB devices.
5. Make two clean builds from the same locked inputs. Compare DLL bytes/hashes,
   headers, normal/delay imports and exports. Investigate mismatches; do not call
   reproducibility proved merely because both builds compile. Record per-file source,
   patch, static runtime and license provenance, and package matching source/notices.
6. Only after approval create the final application allowlist/hash manifest and
   implement constrained loading and reviewed interop. Add the requested fake/synthetic
   loader/hash/PE/symbol/SafeHandle/metadata/privacy tests before enabling production.
   Repeat Cardryft validation and **stop before hardware use**.

## Interop and lifetime gates after artifacts

No P/Invoke surface is implemented in 4C. A future surface must limit UTF-8 keys to
DeviceName/ProductType/ProductVersion/BuildVersion, USB-only options and matched
owned frees; no generic public key/domain API, Pair/Unpair/handshake helper or service
enumeration. Existing-trust session setup still needs a reviewed call graph.
[1.4.0 lockdown source](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/src/lockdown.c)
contains automatic pairing in the handshake constructor; even its non-handshake
constructor implicitly reads DeviceClass/ProductVersion. Do not silently approve
additional reads, trust inference or pair-record repair.

[Property-list receive source](https://github.com/libimobiledevice/libimobiledevice/blob/1.4.0/src/property_list_service.c)
uses a 30-second initial receive, then allocates the advertised packet length and
loops over further receives. This does not establish an absolute operation deadline
or a bounded plist allocation. A proposed reviewed patch must cap frames and enforce
total enumeration/connect/read/free deadlines (initial targets: 5 seconds enumeration,
10 seconds per-device operation, 30 seconds per refresh). Native parser/transport/TLS
paths and cleanup need the same audit. These are proposed limits, not implemented facts.

Task.Run or abandoning an await cannot safely terminate C work. Until every native
operation/cleanup is bounded, do not enable an in-process backend. If that cannot be
established, a separately reviewed Cardryft-owned worker process would be an architecture
decision, not permission to invoke external device tools. Stale-result suppression
remains necessary, and modules/handles may be released only after workers retire.

Constrained loading must use approved absolute filenames under app-root/native/win-x64,
reject network/TEMP/Downloads/user-selected roots and reparse/path escapes, validate
hashes/PE architecture/recursive imports/symbols, and use Windows dependency resolution
restricted to that directory and system locations. No PATH mutation, fallback search
or writable external configuration/module paths. Hash checks alone do not prevent
file replacement races or unsafe DLL initialization; preserve ownership through loading
and audit initialization/unload behavior. This policy is a future requirement;
today's loader still returns unavailable and never invokes the OS loader.

Required future tests include allowlist/hashes/PE32-vs-PE32+/wrong machine, unsafe
directory/traversal/reparse/missing/unexpected imports, fake symbol resolution,
checked error mapping, matching native release/duplicate dispose/module lifetime,
typed metadata keys/no pairing surface/no identity persistence, total deadlines and
shutdown with work active. Current fake tests are not evidence for those native gates.
