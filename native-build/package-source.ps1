param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repositoryRoot '.local/native-build'
$runRoot = Join-Path $root "runs/$Label"
if (-not (Test-Path "$runRoot/audit/pe-audit.json")) { throw 'Audit the build first.' }
$destination = Join-Path $runRoot 'release-material'
if (Test-Path $destination) { throw 'Refusing to overwrite source material.' }
New-Item -ItemType Directory -Force "$destination/sources", "$destination/licenses", "$destination/native-build" | Out-Null
$recipeRoot = Join-Path $root "recipes/$Label"
$sourceLock = Get-Content "$runRoot/audit/sources-lock.json" -Raw | ConvertFrom-Json
$toolLock = Get-Content "$runRoot/audit/toolchain-lock.json" -Raw | ConvertFrom-Json
foreach ($source in @($sourceLock.sources) + @($toolLock.correspondingSource)) {
    $path = Join-Path "$root/downloads" $source.file
    if ((Get-FileHash $path).Hash.ToLowerInvariant() -ne $source.sha256) { throw 'Corresponding source mismatch.' }
    Copy-Item -LiteralPath $path -Destination "$destination/sources"
}
Copy-Item -LiteralPath "$repositoryRoot/THIRD-PARTY-NOTICES.md" -Destination $destination
Copy-Item -Path "$recipeRoot/*" -Destination "$destination/native-build" -Recurse
Copy-Item -Path "$recipeRoot/licenses/*" -Destination "$destination/licenses"
Copy-Item -LiteralPath "$runRoot/audit/pe-audit.json" -Destination $destination
# Preserve complete upstream notices/disclaimers (including file-specific MIT and
# LibTomCrypt permission notices), rather than reducing everything to SPDX labels.
foreach ($source in $sourceLock.sources) {
    $directory = Join-Path "$runRoot/src" "$($source.name)-$($source.version)"
    $target = Join-Path "$destination/licenses" $source.name
    New-Item -ItemType Directory -Force $target | Out-Null
    Get-ChildItem $directory -File | Where-Object { $_.Name -match '^(COPYING|LICENSE|NOTICE|AUTHORS)' } | ForEach-Object { Copy-Item $_.FullName $target }
}
foreach ($name in @('crt','headers','libgcc')) {
    Copy-Item -LiteralPath "$root/toolchain/msys64/ucrt64/share/licenses/$name" -Destination "$destination/licenses" -Recurse
}
Get-ChildItem $destination -File -Recurse | Sort-Object FullName | ForEach-Object {
    [PSCustomObject]@{file=$_.FullName.Substring($destination.Length+1).Replace('\','/');sha256=(Get-FileHash $_.FullName).Hash.ToLowerInvariant()}
} | ConvertTo-Json -Depth 4 | Set-Content "$destination/material-hashes.json" -Encoding utf8
Write-Output "Matching source and notice material staged: $destination. This does not approve runtime promotion."
