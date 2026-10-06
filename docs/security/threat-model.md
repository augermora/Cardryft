# Threat model: Milestone 1

## Scope and assets

The current application displays a static WinForms window. Core validates an
artwork-project display name in memory. No project storage, file import/export,
image decoding, backups, device communication, or Apple Wallet access exists.
Tests use synthetic artwork names. No payment or device records are collected.

Assets to protect are future user artwork and project metadata, user privacy,
repository integrity, and build dependency integrity. Artwork and display names
can themselves reveal personal information; their future handling must be reviewed.

## Trust boundaries

- App will be the boundary for user input; Core already rejects null/blank names.
- Future image and project-file imports will cross from untrusted local files.
- Future persistence and backups will cross into the user's filesystem.
- NuGet restore crosses a development-time network boundary to obtain test
  dependencies. It is distinct from application network communication.
- Device and Wallet boundaries are inactive and prohibited in this milestone.

## Threats, current controls, and remaining work

| Threat | Current control | Remaining risk or future requirement |
| --- | --- | --- |
| Collection or disclosure of payment/device secrets | No relevant data fields, integrations, pairing, network clients, or telemetry | Do not add card numbers, CVVs, PINs, bank credentials, or pairing storage |
| Malformed images or project files | No file loading or decoding exists | Review formats, size limits, parser behavior, and library choice before import |
| Unsafe paths, overwrite, or backup disclosure | No persistence or backups exist | Review path handling, user consent, permissions, and recovery before storage |
| Sensitive artifacts entering Git | Ignore rules cover local secrets, .env files, certificates, pairing/device directories, backups, and dumps | Ignore patterns cannot protect already tracked or differently named files; inspect diffs and untracked files |
| Elevated access | App manifest requests asInvoker; no privileged operations | Review any future capability that requests broader access |
| Dependency compromise | No production packages; three version-pinned test packages, restored through NuGet; xUnit v3 uses the mtp-off variant to exclude Microsoft Testing Platform and its telemetry dependencies | Pinning does not establish safety; review dependency changes and transitive packages |
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
It does not protect against a compromised host, encrypt data, validate images,
secure backups, or claim secure Wallet integration. There is currently no
persistent application data to protect. Revisit this document before expanding
any input, storage, or integration surface.
