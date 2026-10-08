# Cardryft native build pipeline (Milestone 4D)

This pipeline builds audit candidates. It does not enable Cardryft's native backend,
install drivers, enumerate USB devices, contact usbmux/lockdown, or access an iPhone.
Do not copy results into `native/win-x64` until all review gates pass.

## Inputs and bootstrap trust

`sources-lock.json` pins upstream repositories, commits/tags, archive hashes, licenses,
build systems and every project patch. `toolchain-lock.json` pins the official MSYS2
2026-09-27 portable base plus the exact compiler/tool package archive versions/hashes.
Repository databases were used once to resolve that lock; rebuilds never consult them.
The lock also includes corresponding GCC/MinGW source archives. No installer or package
install script is executed. There is no `pacman -Syu`, live package resolution, Git clone,
machine PATH search, global installation, administrator requirement, or downloaded
device-tool invocation.
Repository `.gitattributes` keeps native-build text/patches LF across Windows checkouts;
it does not alter the user's Git configuration.

Bootstrap trust is the official HTTPS publishers and recorded SHA-256 values;
these do not independently authenticate a publisher or prove compiler bootstrapping.
Detached signature verification and rebuilding the compiler itself are not claimed.
If an archive disappears, acquisition fails; update/review the lock explicitly rather
than substituting a different version. Never execute an archive whose hash differs.

## Commands

Run from `C:\Dev\Cardryft` using PowerShell on Windows x64:

```powershell
.\native-build\acquire.ps1
.\native-build\prepare.ps1
.\native-build\build.ps1 -Label E
.\native-build\build.ps1 -Label F
.\native-build\audit.ps1 -Label E
.\native-build\audit.ps1 -Label F
.\native-build\compare.ps1 -BuildA E -BuildB F
.\native-build\package-source.ps1 -Label E
.\scripts\validate.ps1
```

Acquisition is the only native step that uses the network. All later steps operate
on locked local inputs. Scripts refuse existing toolchain/build/source-package paths.
Use fresh labels for additional clean builds. Never remove an unchecked computed path.
On resume, verify and reuse the prepared toolchain/downloads. Inspect logs, frozen recipes
and staged PE files before accepting interrupted steps. Do not reacquire/reprepare or
overwrite existing runs. E/F are the final labels for this milestone; earlier A–D
attempts remain available as diagnostic evidence and are not successful final builds.
Generated toolchain, downloads, work, staging, logs, caches, homes, AppData and temporary
files remain under ignored `.local/native-build`. The application runtime is untouched.

The build runner starts an explicit bash executable without profiles in a fresh
child environment. PATH contains the staging dependency directory, locked UCRT64/MSYS
tools and Windows System32. HOME/USERPROFILE/AppData/TEMP/TMP belong to that build's
repository-local state. LANG/LC_ALL=C, TZ=UTC, SOURCE_DATE_EPOCH=1790294400, CONFIG_SITE=/dev/null,
and explicit pkg-config directories prevent ambient configuration. Parent variables
are unchanged. The process runs without a visible console. The Codex restricted process
sandbox could not initialize MSYS; executing this controlled build outside that sandbox
was required here, without requesting Windows elevation.

Windows tar extracts the base. MSYS bsdtar handles package links without privileges;
compiler executable links are materialized as ordinary files inside the verified tree.
The base's package records and the exact overlay lock together describe the toolchain;
we do not pretend extracting packages updated pacman's installed database.
Preparation copies the pinned UCRT pkgconf macro into MSYS aclocal's search directory,
because no profile is read to set ACLOCAL_PATH. No additional macro package is resolved.

## Recipe and review

See `build.sh` for the exact executable commands; logs record every command and argument.
The order is OpenSSL → plist C library → glue → usbmux client library → libimobiledevice.
No libtatsu/curl, C++ plist library, Python, readline, GNU usbmuxd, Apple DLL, CLI utility,
wireless pairing helper, app/content service, engine, provider module or zlib runtime
is selected. Build-tool transitive packages are not application dependencies.

GCC optimizes with `-O2 -D_WIN32_WINNT=0x0A00`, uses function/data sections and remaps file/debug/macro paths
to `/cardryft-build`; linking uses `--no-insert-timestamp`, `--build-id=none`,
`--gc-sections` and `-static-libgcc`. OpenSSL's configured logical install/config prefix
is constant; DESTDIR redirects installation into each independent build's staging tree.
One OpenSSL patch canonicalizes remapping flags in informational compiler text only.
The other enables the Windows system-preferred BCrypt RNG for MinGW, replacing the
legacy registry-selected CryptoAPI provider path. `--with-rand-seed=os` preserves OS
entropy; `-lbcrypt` is a final Configure library argument so GNU ld resolves it after
the provider archive. No cryptographic algorithm or TLS policy is weakened.
The libimobiledevice patches exclude unused services/tools, remove the tools-only tatsu
configure prerequisite and give the nine-symbol `.def` sole control over Windows
exports by removing blanket internal `dllexport` annotations. They change no protocol code.
Glue/usbmux patches match the pinned MinGW `libiphlpapi.a` filename; otherwise libtool
silently falls back to static libraries. Every applied patch is SHA-256 locked.

OpenSSL uses `shared no-autoload-config no-dso no-module no-engine no-comp no-zlib
no-legacy no-sock no-apps no-tests`; the default provider is built in. Removing socket BIOs
does not remove libimobiledevice's transport BIO. No recipe security-level or certificate
verification downgrade is added. This is not a proof of TLS/device compatibility.
`make -j4 build_libs LIBS=` builds shared libraries/import libraries and their prerequisites,
avoiding a second unused static OpenSSL compilation. `make DESTDIR=... LIBS= INSTALL_LIBS=
install_dev` stages the shared/import/header/pkg-config outputs. The runner supplies valid
closed stdin and the shell uses `/dev/null`; libtool must not inherit an invalid descriptor.

`inspect-pe.ps1` reads bytes without loading DLLs. `audit.ps1` requires exactly six
AMD64/PE32+ DLLs, zero PE timestamps/delay imports, a closed non-system dependency graph,
an explicit Windows/UCRT import allowlist and the exact root export list. It rejects
extras, missing files, wrong architecture, unexpected imports or exports. All named
exports, source/patch identities, sizes and SHA-256 values are in `audit/pe-audit.json`;
independent GNU objdump output is retained too. This build reader is not a production
untrusted-file parser or OS loader. PE closure cannot prove absence of dynamic loading.

`compare.ps1` compares bytes as well as recording hashes, and fails on differences.
`package-source.ps1` stages matching source archives, patches, scripts, complete license
texts/notices and a material hash inventory using the build's frozen recipe/locks.
Audit/comparison/source packaging refuse existing evidence destinations.
It copies no runtime DLLs and publishes
nothing. Release compliance still requires the application source/relinking route,
notice review and any later interop modifications to accompany the released binaries.

See the [measured evidence and remaining gates](../docs/research/native-dependency-manifest.md)
and [licensing ledger](../THIRD-PARTY-NOTICES.md). These scripts never promote artifacts.
The 2026-10-08 E/F comparison passed for all six DLLs; complete imports/exports,
hashes and frozen input identities are retained in
[reviewable text evidence](evidence/milestone4d-results.json). Each native build has
six unsuppressed upstream warning lines and zero errors. That evidence is not a
production loader manifest; security, ABI/lifetime and LGPL replacement gates remain.
