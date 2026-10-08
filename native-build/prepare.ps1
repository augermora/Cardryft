$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repositoryRoot '.local/native-build'
$toolRoot = Join-Path $localRoot 'toolchain/msys64'
$lock = Get-Content "$PSScriptRoot/toolchain-lock.json" -Raw | ConvertFrom-Json
if (Test-Path -LiteralPath $toolRoot) { throw 'Refusing to overwrite an existing toolchain. Use the verified prepared snapshot or a new clean workspace.' }
foreach ($item in @($lock.base) + @($lock.packages)) {
    $path = Join-Path "$localRoot/downloads" $item.file
    if ((Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -ne $item.sha256) { throw "Input hash mismatch: $($item.file)" }
}
New-Item -ItemType Directory -Force "$localRoot/toolchain" | Out-Null
& "$env:SystemRoot/System32/tar.exe" -xf "$localRoot/downloads/$($lock.base.file)" -C "$localRoot/toolchain"
if ($LASTEXITCODE -ne 0) { throw 'Base extraction failed.' }
# MSYS archive handling emulates symlinks without administrator privileges.
# Windows tar cannot create the MinGW package's symlinks under asInvoker.
[IO.File]::WriteAllText("$localRoot/package-list.txt", (($lock.packages.file -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
. "$PSScriptRoot/invoke-msys.ps1"
Invoke-CardryftMsys -ToolRoot $toolRoot -Script "$PSScriptRoot/prepare.sh" -ScriptArguments @($repositoryRoot) -StateRoot "$localRoot/state/prepare"
# No login profile is read. Make the pinned UCRT pkgconf macro available to the
# pinned MSYS aclocal without depending on a profile-provided ACLOCAL_PATH.
Copy-Item -LiteralPath "$toolRoot/ucrt64/share/aclocal/pkg.m4" -Destination "$toolRoot/usr/share/aclocal/pkg.m4"
Copy-Item -LiteralPath "$PSScriptRoot/toolchain-lock.json" -Destination "$localRoot/toolchain/installed-lock.json"
Write-Output "Prepared hash-locked toolchain: $toolRoot"
