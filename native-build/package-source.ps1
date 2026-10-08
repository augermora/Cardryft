param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repositoryRoot '.local/native-build'
$runRoot = Join-Path $root "runs/$Label"
if (-not (Test-Path "$runRoot/audit/pe-audit.json")) { throw 'Audit the build first.' }
$destination = Join-Path $runRoot 'release-material'
if (Test-Path $destination) { throw 'Refusing to overwrite source material.' }
New-Item -ItemType Directory -Force "$destination/sources", "$destination/licenses", "$destination/native-build" | Out-Null
New-Item -ItemType Directory -Path "$destination/release-workflows" | Out-Null
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
foreach ($name in @('package-source.ps1','generate-runtime-pins.ps1','replacement-build.sh','build-replacement.ps1','replacement-test.ps1','test-replacement-fixtures.ps1')) {
    if (Test-Path -LiteralPath "$PSScriptRoot/$name") { Copy-Item -LiteralPath "$PSScriptRoot/$name" -Destination "$destination/release-workflows" }
}
Copy-Item -LiteralPath "$repositoryRoot/LICENSE" -Destination "$destination/licenses/Cardryft-MIT.txt"
New-Item -ItemType Directory "$destination/application-source" | Out-Null
foreach ($file in Get-ChildItem "$repositoryRoot/src","$repositoryRoot/tests","$repositoryRoot/scripts","$repositoryRoot/docs" -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) {
    $relative=$file.FullName.Substring($repositoryRoot.Length+1)
    $target=Join-Path "$destination/application-source" $relative
    New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target
}
foreach ($name in @('Cardryft.sln','Directory.Build.props','NuGet.Config','LICENSE','README.md','AGENTS.md','THIRD-PARTY-NOTICES.md')) {
    Copy-Item -LiteralPath "$repositoryRoot/$name" -Destination "$destination/application-source"
}
# The manifest regression test consumes this tracked, non-sensitive fixture.
New-Item -ItemType Directory -Force "$destination/application-source/native-build/evidence" | Out-Null
Copy-Item -LiteralPath "$repositoryRoot/native-build/evidence/milestone4f-results.json" -Destination "$destination/application-source/native-build/evidence"
Copy-Item -LiteralPath "$runRoot/audit/pe-audit.json" -Destination $destination
# Preserve complete upstream notices/disclaimers (including file-specific MIT and
# LibTomCrypt permission notices), rather than reducing everything to SPDX labels.
foreach ($source in $sourceLock.sources | Where-Object { -not $sourceLock.buildSources -or $_.name -in $sourceLock.buildSources }) {
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
