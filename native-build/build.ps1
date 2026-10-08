param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repositoryRoot '.local/native-build'
$toolRoot = Join-Path $localRoot 'toolchain/msys64'
if ((Get-FileHash "$localRoot/toolchain/installed-lock.json").Hash -ne (Get-FileHash "$PSScriptRoot/toolchain-lock.json").Hash) { throw 'Toolchain lock changed; prepare a clean toolchain.' }
$lock = Get-Content "$PSScriptRoot/sources-lock.json" -Raw | ConvertFrom-Json
foreach ($item in $lock.sources) {
    if ((Get-FileHash "$localRoot/downloads/$($item.file)").Hash.ToLowerInvariant() -ne $item.sha256) { throw "Source changed: $($item.name)" }
}
foreach ($patch in $lock.patches) {
    if ((Get-FileHash "$PSScriptRoot/$($patch.file)").Hash.ToLowerInvariant() -ne $patch.sha256) { throw "Patch changed: $($patch.file)" }
}
foreach ($source in $lock.localSources) {
    if ((Get-FileHash "$PSScriptRoot/$($source.file)").Hash.ToLowerInvariant() -ne $source.sha256) { throw "Local source changed: $($source.file)" }
}
. "$PSScriptRoot/invoke-msys.ps1"
$recipeRoot = Join-Path $localRoot "recipes/$Label"
if (Test-Path $recipeRoot) { throw 'Refusing to reuse an existing recipe snapshot.' }
New-Item -ItemType Directory -Path $recipeRoot | Out-Null
Copy-Item -Path "$PSScriptRoot/*" -Destination $recipeRoot -Recurse
$inputNames = @('build.sh','invoke-msys.ps1','sources-lock.json','toolchain-lock.json') + @($lock.patches.file) + @($lock.localSources.file)
$inputHashes = foreach ($name in $inputNames) { [ordered]@{file=$name;sha256=(Get-FileHash "$recipeRoot/$name").Hash.ToLowerInvariant()} }
[IO.File]::WriteAllText("$recipeRoot/build-inputs.json", ($inputHashes | ConvertTo-Json -Depth 5)+"`n", [Text.UTF8Encoding]::new($false))
Invoke-CardryftMsys -ToolRoot $toolRoot -Script "$recipeRoot/build.sh" -ScriptArguments @($repositoryRoot, $Label, $recipeRoot) -StateRoot "$localRoot/state/$Label"
Write-Output "Build $Label completed. Audit staged DLLs before promotion."
