param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$base = Join-Path $repositoryRoot '.local/native-build'
$lock = Get-Content "$PSScriptRoot/sources-lock.json" -Raw | ConvertFrom-Json
$source = $lock.sources | Where-Object name -eq 'libplist'
$patch = $lock.patches | Where-Object file -eq 'patches/libplist-cardryft-bounds.patch'
if ((Get-FileHash "$base/downloads/$($source.file)").Hash.ToLowerInvariant() -ne $source.sha256 -or
    (Get-FileHash "$PSScriptRoot/$($patch.file)").Hash.ToLowerInvariant() -ne $patch.sha256 -or
    (Get-FileHash "$base/toolchain/installed-lock.json").Hash -ne (Get-FileHash "$PSScriptRoot/toolchain-lock.json").Hash) { throw 'Locked replacement inputs mismatch.' }
. "$PSScriptRoot/invoke-msys.ps1"
Invoke-CardryftMsys -ToolRoot "$base/toolchain/msys64" -Script "$PSScriptRoot/replacement-build.sh" -ScriptArguments @($repositoryRoot,$Label) -StateRoot "$base/state/$Label-replacement"
Write-Output "Recipient-modified LGPL library staged under .local/native-build/replacements/$Label. No app policy changed."
