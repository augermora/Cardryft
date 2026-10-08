param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repositoryRoot '.local/native-build'
$target = Join-Path $localRoot "reverification/$Label"
if (Test-Path -LiteralPath $target) { throw 'Refusing to overwrite reverification evidence.' }
for ($ancestor = $target; $ancestor; $ancestor = Split-Path -Parent $ancestor) {
    if ((Test-Path -LiteralPath $ancestor) -and
        (Get-Item -LiteralPath $ancestor -Force).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
        throw 'Reverification paths must not contain reparse points.'
    }
}
New-Item -ItemType Directory -Path $target | Out-Null
$evidence = Get-Content "$PSScriptRoot/evidence/milestone4f-results.json" -Raw | ConvertFrom-Json
$lock = Get-Content "$PSScriptRoot/sources-lock.json" -Raw | ConvertFrom-Json
function Assert-Hash([string]$Path, [string]$Expected) {
    if ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $Expected) {
        throw "Hash mismatch: $Path"
    }
}
function Assert-SameArray($Expected, $Actual) {
    $first = @($Expected | Sort-Object)
    $second = @($Actual | Sort-Object)
    if ($first.Count -ne $second.Count -or ($first -join "`0") -cne ($second -join "`0")) {
        throw 'PE array differs from committed 4F evidence.'
    }
}
Assert-Hash "$repositoryRoot/LICENSE" $evidence.licenseSha256
Assert-Hash "$localRoot/toolchain/installed-lock.json" (Get-FileHash "$PSScriptRoot/toolchain-lock.json").Hash.ToLowerInvariant()
foreach ($source in $lock.sources) { Assert-Hash "$localRoot/downloads/$($source.file)" $source.sha256 }
foreach ($source in @($lock.patches) + @($lock.localSources)) { Assert-Hash "$PSScriptRoot/$($source.file)" $source.sha256 }
$reports = @()
foreach ($build in @('4F-C', '4F-D')) {
    $run = Join-Path $localRoot "runs/$build"
    $recipe = Join-Path $localRoot "recipes/$build"
    $inputs = Get-Content "$recipe/build-inputs.json" -Raw | ConvertFrom-Json
    foreach ($input in $inputs) {
        Assert-Hash "$recipe/$($input.file)" $input.sha256
        Assert-Hash "$PSScriptRoot/$($input.file)" $input.sha256
    }
    foreach ($source in $lock.localSources) { Assert-Hash "$run/$($source.file)" $source.sha256 }
    $files = @(Get-ChildItem -LiteralPath "$run/runtime" -Force)
    if ($files.Count -ne 4 -or @($files | Where-Object PSIsContainer).Count) { throw 'Unexpected runtime entry set.' }
    Assert-SameArray $evidence.runtimeFiles.File $files.Name
    $pe = foreach ($pin in $evidence.runtimeFiles) {
        $path = "$run/runtime/$($pin.File)"
        Assert-Hash $path $pin.SHA256
        $actual = & "$PSScriptRoot/inspect-pe.ps1" -Path $path | ConvertFrom-Json
        foreach ($property in @('File','Size','SHA256','Machine','OptionalHeader','CoffTimestamp','ExportFunctionCount')) {
            if ($actual.$property -ne $pin.$property) { throw "Changed PE field: $property" }
        }
        foreach ($property in @('Imports','DelayImports','Exports','Forwarders')) {
            Assert-SameArray $pin.$property $actual.$property
        }
        $actual
    }
    $reports += [ordered]@{build=$build;files=@($pe);frozenInputsVerified=$true}
}
Assert-Hash "$localRoot/recipes/4F-D/build-inputs.json" (Get-FileHash "$localRoot/recipes/4F-C/build-inputs.json").Hash.ToLowerInvariant()
$comparison = foreach ($pin in $evidence.runtimeFiles) {
    $first = [IO.File]::ReadAllBytes("$localRoot/runs/4F-C/runtime/$($pin.File)")
    $second = [IO.File]::ReadAllBytes("$localRoot/runs/4F-D/runtime/$($pin.File)")
    if (-not [Collections.StructuralComparisons]::StructuralEqualityComparer.Equals($first, $second)) { throw 'Clean-build bytes differ.' }
    [ordered]@{file=$pin.File;identical=$true;sha256=$pin.SHA256;size=$pin.Size}
}
$package = [IO.Path]::GetFullPath("$localRoot/runs/4F-D/release-material")
Assert-Hash "$package/material-hashes.json" $evidence.sourcePackageVerification.materialInventorySha256
$materials = @(Get-Content "$package/material-hashes.json" -Raw | ConvertFrom-Json)
foreach ($material in $materials) {
    $path = [IO.Path]::GetFullPath((Join-Path $package $material.file))
    if (-not $path.StartsWith($package + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Source material path escaped the preserved package.'
    }
    Assert-Hash $path $material.sha256
}
if (@(Get-ChildItem -LiteralPath $package -Recurse -File | Where-Object Extension -in @('.dll','.exe','.pdb')).Count) {
    throw 'Unexpected binary in preserved corresponding source.'
}
# Only the already source-built, memory-only fixture is executed. No native
# enumeration/open/query, Apple protocol, listener observation or record access.
$fixtures = foreach ($build in @('4F-C', '4F-D')) {
    $run = "$localRoot/runs/$build"
    $directory = Join-Path $target $build
    New-Item -ItemType Directory -Path $directory, "$directory/tmp" | Out-Null
    Copy-Item -LiteralPath "$run/shim/native-fixtures.exe" -Destination $directory
    Assert-Hash "$directory/native-fixtures.exe" (Get-FileHash "$run/shim/native-fixtures.exe").Hash.ToLowerInvariant()
    foreach ($name in @('libcrypto-3-x64.dll','libssl-3-x64.dll','libplist-2.0.dll')) {
        Copy-Item -LiteralPath "$run/runtime/$name" -Destination $directory
        Assert-Hash "$directory/$name" ($evidence.runtimeFiles | Where-Object File -eq $name).SHA256
    }
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = "$directory/native-fixtures.exe"
    $start.WorkingDirectory = $directory
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.Environment.Clear()
    $start.Environment['SystemRoot'] = $env:SystemRoot
    $start.Environment['WINDIR'] = $env:SystemRoot
    $start.Environment['PATH'] = "$env:SystemRoot\System32"
    $start.Environment['TEMP'] = "$directory/tmp"; $start.Environment['TMP'] = "$directory/tmp"
    $process = [Diagnostics.Process]::Start($start)
    try {
        $process.StandardInput.Close()
        $stdout = $process.StandardOutput.ReadToEndAsync(); $stderr = $process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit(30000)) {
            $process.Kill($true)
            throw 'Isolated synthetic fixture exceeded its test deadline.'
        }
        $output = $stdout.GetAwaiter().GetResult() + $stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText("$directory/fixtures.txt", $output, [Text.UTF8Encoding]::new($false))
        if ($process.ExitCode -ne 0 -or $output -notmatch '43 passed, 0 failed') { throw 'Offline fixture failed.' }
        Write-Host "$build`: $($output.Trim())"
        [ordered]@{build=$build;passed=43;failed=0;exitCode=$process.ExitCode;deviceOperations=0}
    }
    finally { $process.Dispose() }
}
# Verify, rather than rebuild, the unchanged LGPL replacement and its old probes.
$replacement = "$localRoot/replacements/4F-LGPL/prefix/bin/libplist-2.0.dll"
Assert-Hash $replacement $evidence.replacementFixtures.modifiedDllSha256
$modified = & "$PSScriptRoot/inspect-pe.ps1" -Path $replacement | ConvertFrom-Json
$plist = $evidence.runtimeFiles | Where-Object File -eq 'libplist-2.0.dll'
foreach ($property in @('Imports','DelayImports','Exports','Forwarders')) { Assert-SameArray $plist.$property $modified.$property }
if ($modified.Machine -ne '0x8664' -or $modified.OptionalHeader -ne '0x020b' -or $modified.CoffTimestamp -ne 0) {
    throw 'Replacement PE identity changed.'
}
$priorTrials = Get-Content "$localRoot/replacement-tests/4F-runtime-trials-final/results.json" -Raw | ConvertFrom-Json
if ($priorTrials.modified -ne $modified.SHA256 -or -not $priorTrials.officialHashPolicyUnchanged -or
    $priorTrials.runtimePromoted -or @($priorTrials.results | Where-Object { $_.exitCode -ne $_.expected -or $_.deviceOperations -ne 0 }).Count) {
    throw 'Preserved replacement experiment is inconsistent.'
}
if (Test-Path -LiteralPath "$repositoryRoot/native/win-x64") { throw 'Runtime unexpectedly promoted.' }
$result = [ordered]@{
    schemaVersion=1;purpose='4G stopped-gate reverification; not runtime approval'
    runtimePromoted=$false;hardwareReady=$false;nativeSourceChanged=$false;freshBuilds=0
    audits=$reports;byteComparison=@($comparison);nativeFixtures=@($fixtures)
    correspondingSourceFiles=$materials.Count;correspondingSourceHashesVerified=$true
    replacementSha256=$modified.SHA256;replacementPEVerified=$true
    priorReplacementResultsVerified=$true;replacementProbesRerun=$false
    licenseSha256=$evidence.licenseSha256
}
[IO.File]::WriteAllText("$target/results.json", ($result | ConvertTo-Json -Depth 12) + "`n", [Text.UTF8Encoding]::new($false))
Write-Output 'Eight DLLs rehashed/PE inspected; four clean-build pairs byte-identical; source package and LGPL evidence reverified. Promotion remains blocked.'
