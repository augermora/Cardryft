# Third-party native dependency notices and compliance ledger

**Milestones 4C–4D: native audit/build material, not an approved runtime release.**
Cardryft's own source remains MIT under LICENSE. This document does not relicense
third-party code or assert completed distribution compliance. Existing managed
NuGet dependencies are unchanged; their package licenses remain applicable.

The [native evidence manifest](docs/research/native-dependency-manifest.md) records
candidate binary/package/source hashes, revisions, import relationships and the
blocked runtime gate. Audit packages/source and six extracted DLLs are retained
only as inert, ignored `.local/milestone4c-audit` evidence. No Apple component is
copied, bundled or licensed by Cardryft.

## Milestone 4D source-built selection

The [locked build](native-build/README.md) uses upstream libimobiledevice 1.4.0,
libusbmuxd 2.1.1, libplist C 2.7.0, glue 1.3.2 and OpenSSL 3.6.5. Exact commits,
archive/patch hashes and build systems are in [sources-lock.json](native-build/sources-lock.json);
compiler/package hashes and source archives are in [toolchain-lock.json](native-build/toolchain-lock.json).
This selection does not use the 4C MSYS2 OpenSSL relocation/CC0 helper patches,
libtatsu/curl, GPL iproxy/inetcat/usbmuxd/readline, C++/Python wrappers or Apple components.

| Selected code | Exact license basis / notices | Distribution and source/rebuild action |
| --- | --- | --- |
| libimobiledevice (including common/userpref), libusbmuxd client C | Source headers LGPL-2.1-or-later | Select the LGPL-3.0 route for this combination; retain original notices and full LGPL/GPL texts, matching source, changed build recipe and export list |
| libplist C and compiled libcnary | LGPL-2.1-or-later headers, with MIT file exceptions | Choose LGPL-3.0 for combined library; preserve Michael G Schwern's time64 and Serge A. Zaitsev's jsmn MIT notices in matching source/release attribution material |
| libimobiledevice-glue | LGPL-2.1-or-later; SHA files retain Tom St Denis/LibTomCrypt's express free-for-all-purposes permission and no-warranty notice | Choose LGPL-3.0 for combined library; preserve the exact additional SHA notices, without assigning an unsupported SPDX label to that permission |
| OpenSSL | Apache-2.0; copyright headers and LICENSE.txt | Include full Apache text and source notices; mark Cardryft's informational build-text and MinGW system-preferred BCrypt RNG modifications. No custom external provider/engine or MSYS path helper is compiled |
| Statically linked libgcc support, GCC 16.2.0-4 | GPL-3.0-or-later WITH GCC-exception-3.1; exact COPYING.RUNTIME/COPYING3 from locked package/source | Compilation uses GCC with ordinary C/assembler inputs, no non-GPL compiler/intermediate-code plugin. Preserve exception/license notices and corresponding package source/patches. Exception eligibility permits independent modules under their own terms; it does not waive LGPL obligations |
| MinGW-w64 CRT/headers, 14.0.0.r426.g4564ee4b5-1 | Revision 4564ee4b5063097bf747af3a3f8270a28adff820; ZPL-2.1 overall plus file-specific permissive/public-domain terms | Include complete COPYING.MinGW-w64-runtime.txt and package notices, not only the umbrella SPDX label; exact CRT/header source archives and recipes are locked |
| Windows system/UCRT imports | OS prerequisites, not Cardryft redistributables | Do not copy local Windows/Apple DLLs; the future loader must resolve system dependencies only through approved OS locations |

Source headers, rather than package-wide GPL labels, establish the selected plist
library's terms. Its libcnary object code is included and audited as LGPL-2.1-or-later.
No GPL-only device tool is linked into the selected libraries. Source archives may
contain excluded utilities with their original licenses; supplying their unchanged
source does not make them runtime components or authorize shipping their executables.
Cardryft's dated/reviewable patch records also identify the Windows export annotation
change and glue/usbmux library filename fixes. Original copyright/license context is
preserved. No protocol behavior is modified by these library build/export patches.
Cardryft modified the upstream build/export files on **2026-10-07–2026-10-08**:
libimobiledevice `Makefile.am`, `configure.ac`, `src/Makefile.am`, `src/idevice.h`;
glue/usbmux `src/Makefile.am` and `src/Makefile.in`; OpenSSL `util/mkbuildinf.pl`
and `providers/implementations/rands/seeding/rand_win.c`. The matching source lock
records each patch and SHA-256. This notice accompanies the original archives and
separate patches; source archives themselves are unchanged.

Apache-2.0 compatibility is addressed by selecting the libraries' explicit "or later"
permission and applying LGPL-3.0 combined-work conditions, not claiming an
LGPL-2.1-only/Apache combination. The MIT application source stays MIT. See
[LGPL 3 section 4](https://www.gnu.org/licenses/lgpl-3.0.html) and the
[GCC Runtime Library Exception 3.1](https://www.gnu.org/licenses/gcc-exception-3.1.html).
Full applicable license texts are preserved under `native-build/licenses`; original
upstream texts and copyrights accompany matching source. This is concrete engineering
compliance preparation, not a legal guarantee.
OpenSSL redistribution and change notices follow the
[Apache 2.0 redistribution terms](https://www.apache.org/licenses/LICENSE-2.0).

`package-source.ps1` verifies and stages exact corresponding sources, toolchain-runtime
source packages, patches, build scripts/locks, license texts and notices beside the
audit. The GCC source package contains the upstream GCC 16.2.0 archive and recipe
patches; CRT/header source packages contain the exact upstream Git objects and recipe.
Their source-package SHA-256 pins cover all contents. Individual recipe/patch hashes
are additionally recorded in `native-build/toolchain-source-evidence.json`.
The compiler's own executable/tool-only packages are not shipped with Cardryft;
redistributing that toolchain would require a separate complete tool-license inventory.

Before any binary release, distribute the complete MIT application source and matching
library source/build/relinking information with equivalent download access. Include
notice of LGPL use and reverse-engineering/modification rights. A user must be able
to change compatible library source, rebuild it with this recipe in a fresh workspace,
regenerate pins in a modified application and install that modified application.
The source route is LGPL-3.0 section 4(d)(0), not an assertion that exact-hash official
loading automatically permits replacement under section 4(d)(1). No production
manifest/pin generator or interop exists yet, so that end-to-end modified-application
installation route cannot be tested in 4D and remains a release gate. Official builds
must retain fail-closed loading; do not add an unsigned DLL override as a shortcut.

Earlier 4C planning/candidate evidence follows for traceability; its mixed-version
binary hashes and optional-module license rows do not describe the 4D source build.

## Native component ledger

| Candidate component | License evidence | Release action |
| --- | --- | --- |
| libimobiledevice, libusbmuxd, libplist C library, libimobiledevice-glue | LGPL-2.1-or-later in inspected headers/package evidence | Inventory every compiled source and static helper; preserve copyrights/disclaimers and chosen license texts; supply matching source/patches/build scripts |
| OpenSSL 3.6.5 | Apache-2.0 | Include license and required existing notices/attributions; mark modifications; audit compatibility with the selected LGPL version |
| MSYS2 OpenSSL path helper | CC0 in inspected pathtools.c/h | Preserve provenance and public-domain dedication/disclaimer evidence |
| Statically incorporated GCC/MinGW support | Exact files not yet inventoried; GCC commonly uses GPL-3.0-or-later with Runtime Library Exception, MinGW has file-specific terms | Establish actual objects, licenses and exception eligibility; do not assume absence of a compiler DLL means no obligation |
| Dynamic providers/engines/compression | Not selected or approved | Remove by reviewed build configuration or audit every required artifact and loading path |
| libtatsu/curl and native build tools | Build-only proposal; no runtime inclusion established | Lock/review source/tool licenses separately; include only if measured runtime imports justify it |

The [libusbmuxd upstream license statement](https://github.com/libimobiledevice/libusbmuxd/blob/2.1.1/README.md#license)
distinguishes LGPL library code from GPL iproxy/inetcat utilities. GNU usbmuxd,
iproxy/inetcat and readline are unnecessary runtime selections for this Windows
feature; exclude them rather than distribute GPL tools incidentally.
MSYS2 labels the entire plist package GPL AND LGPL, while inspected library and
plistutil source headers say LGPL. Reconcile actual compiled files before release;
package-wide labels alone neither prove a DLL is GPL nor excuse a source audit.

## LGPL distribution and user modification

The concrete release plan is to distribute shared libraries separately, with
prominent component/version/license notices, intact copyright and warranty notices,
full applicable license texts, and complete corresponding library source, patches,
configuration and compilation/install scripts beside every binary release.
Do not rely solely on a link to a moving upstream branch. Maintain equivalent
source access for downloadable releases; avoid a written-source-offer strategy
unless its fulfillment and duration are deliberately supported.

The [LGPL 2.1 text](https://www.gnu.org/licenses/old-licenses/lgpl-2.1.de.html)
requires notice/source and combined-work conditions. Dynamic linking alone is not
a waiver. A suitable shared-library mechanism must permit compatible modified
libraries; terms must permit the relevant modification/reverse engineering.

Hash-pinned official loading may reject a user's modified library. Do not claim it
automatically satisfies the shared-library replacement route. Provide the complete
MIT application source, matching interop/build tooling and documented local rebuild
steps that regenerate pins for a compatible modified library and produce an installable
modified application at the same controlled directory. Test that route without
removing restrictions from official builds. Review the applicable source/relinking
route, including LGPL 2.1 section 6 or LGPL 3 section 4, before distribution.

OpenSSL 3 is Apache-2.0. Resolve compatibility explicitly for the LGPL combination;
the library headers' "or later" permission may permit an appropriate LGPL/GPL v3
route, subject to every incorporated file. Do not label an Apache-2.0 combination
LGPL-2.1-only or assume Cardryft's MIT license resolves that issue. Preserve CC0
helper provenance and verify any GCC exception from the exact runtime sources.
This is a compliance work plan, not a legal guarantee or release approval.

## Required release structure

Prepare the following with the exact approved build, not candidate placeholders:

```text
THIRD-PARTY-NOTICES.md                 final per-component notices/credits
licenses/<component>/                applicable complete texts/notices
sources/<component>/                 exact corresponding source and patches
sources/native-build/                locked tools, flags, scripts, rebuild instructions
native/win-x64/                      approved DLLs only
native-provenance.json               hashes, revisions, PE imports, license/source mapping
```

This structure is proposed; it is not populated or packaged in 4C. Validate a
source build and a user-modified-library rebuild before describing compliance as
complete. No Apple DLLs/driver packages, CLI tools, pairing records, device data or
private credentials belong in it. Apple USB support remains separately installed
through official vendor channels, outside Cardryft's redistribution.
