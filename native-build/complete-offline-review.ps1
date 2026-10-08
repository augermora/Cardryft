param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$base = Join-Path $repositoryRoot '.local/native-build'
$run = Join-Path $base "runs/$Label"
$recipe = Join-Path $base "recipes/$Label"
$review = Join-Path $run 'fixture-review'
if (Test-Path $review) { throw 'Refusing to overwrite review evidence.' }
if (Get-ChildItem "$run/runtime" -File) { throw 'Review requires an unstaged, preserved build.' }
if ((Get-FileHash "$base/toolchain/installed-lock.json").Hash -ne (Get-FileHash "$recipe/toolchain-lock.json").Hash) { throw 'Toolchain changed.' }
$original = Get-Content "$recipe/sources-lock.json" -Raw | ConvertFrom-Json
$current = Get-Content "$PSScriptRoot/sources-lock.json" -Raw | ConvertFrom-Json
foreach ($source in $original.sources) {
    if ((Get-FileHash "$base/downloads/$($source.file)").Hash.ToLowerInvariant() -ne $source.sha256) { throw 'Source archive changed.' }
}
# Only a fixture correction is permitted. Every input to the runtime DLLs must
# match the clean builds, including the compiled shim and the plist/OpenSSL patches.
foreach ($entry in (Get-Content "$recipe/build-inputs.json" -Raw | ConvertFrom-Json)) {
    if ($entry.file -in @('sources-lock.json','shim/tests.c')) { continue }
    if ((Get-FileHash "$PSScriptRoot/$($entry.file)").Hash.ToLowerInvariant() -ne $entry.sha256) { throw "Runtime input changed: $($entry.file)" }
}
foreach ($source in $current.localSources) {
    if ((Get-FileHash "$PSScriptRoot/$($source.file)").Hash.ToLowerInvariant() -ne $source.sha256) { throw 'Current local source lock mismatch.' }
}
$oldRuntimeLock = $original | ConvertTo-Json -Depth 20
$newRuntimeLock = $current | ConvertTo-Json -Depth 20
$oldRuntimeLock = $oldRuntimeLock.Replace(($original.localSources | Where-Object file -eq 'shim/tests.c').sha256, 'FIXTURE_ONLY')
$newRuntimeLock = $newRuntimeLock.Replace(($current.localSources | Where-Object file -eq 'shim/tests.c').sha256, 'FIXTURE_ONLY')
if ($oldRuntimeLock -ne $newRuntimeLock) { throw 'Sources lock changed beyond the permitted test-only correction.' }
foreach ($source in $original.localSources | Where-Object file -ne 'shim/tests.c') {
    if ((Get-FileHash "$run/$($source.file)").Hash.ToLowerInvariant() -ne $source.sha256) { throw 'Compiled local source does not match frozen input.' }
}
New-Item -ItemType Directory $review | Out-Null
Copy-Item -LiteralPath "$PSScriptRoot/shim/tests.c" -Destination $review
Copy-Item -LiteralPath "$PSScriptRoot/shim/cardryft_device.c","$PSScriptRoot/shim/cardryft_device.h" -Destination $review
$script=@'
set -euo pipefail
root=$(cygpath -u "$1")
label="$2"
run="$root/.local/native-build/runs/$label"
prefix="$run/prefix"
cd "$run/fixture-review"
export PATH="$prefix/bin:$PATH"
gcc -O2 -D_WIN32_WINNT=0x0A00 -Wall -Wextra -Werror -I"$prefix/include" tests.c -o native-fixtures.exe -Wl,--no-insert-timestamp -Wl,--build-id=none -static-libgcc -L"$prefix/lib" -lssl -lcrypto -lplist-2.0 -lws2_32
./native-fixtures.exe >native-fixtures.txt
'@
[IO.File]::WriteAllText("$review/run.sh",$script+"`n",[Text.UTF8Encoding]::new($false))
. "$PSScriptRoot/invoke-msys.ps1"
Invoke-CardryftMsys -ToolRoot "$base/toolchain/msys64" -Script "$review/run.sh" -ScriptArguments @($repositoryRoot,$Label) -StateRoot "$base/state/$Label-fixture-review"
$names=@('cardryft-device.dll','libcrypto-3-x64.dll','libssl-3-x64.dll','libplist-2.0.dll')
foreach ($name in $names) { Copy-Item -LiteralPath "$run/prefix/bin/$name" -Destination "$run/runtime/$name" }
Copy-Item -LiteralPath "$PSScriptRoot/sources-lock.json" -Destination "$review/reviewed-sources-lock.json"
[ordered]@{
    originalBuildInputsSha256=(Get-FileHash "$recipe/build-inputs.json").Hash.ToLowerInvariant()
    correctedFixtureSha256=(Get-FileHash "$review/tests.c").Hash.ToLowerInvariant()
    runtimeRecompiled=$false
    reason='Distinct synthetic certificate subjects and immediate SSL_get_error capture; no runtime input changed.'
    result=(Get-Content "$review/native-fixtures.txt" -Raw).Trim()
} | ConvertTo-Json | Set-Content "$review/completion.json" -Encoding utf8
Get-Content "$review/native-fixtures.txt"
Write-Output 'Existing new clean runtime build completed by corrected offline fixtures; original failed log and recipe remain preserved.'
