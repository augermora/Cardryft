# Third-party native dependency compliance plan

**Milestone 4C planning document. No native runtime is shipped or approved.**
Cardryft's own source remains MIT under LICENSE. This document does not relicense
third-party code or assert completed distribution compliance. Existing managed
NuGet dependencies are unchanged; their package licenses remain applicable.

The [native evidence manifest](docs/research/native-dependency-manifest.md) records
candidate binary/package/source hashes, revisions, import relationships and the
blocked runtime gate. Audit packages/source and six extracted DLLs are retained
only as inert, ignored `.local/milestone4c-audit` evidence. No Apple component is
copied, bundled or licensed by Cardryft.

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
