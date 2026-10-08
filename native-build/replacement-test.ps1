param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Baseline,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Replacement,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$base = Join-Path $repositoryRoot '.local/native-build'
$trial = Join-Path $base "replacement-tests/$Label"
if (Test-Path $trial) { throw 'Refusing to overwrite replacement tests.' }
$baselineRun = Join-Path $base "runs/$Baseline"
$replacementRoot = Join-Path $base "replacements/$Replacement"
$audit = Get-Content "$baselineRun/audit/pe-audit.json" -Raw | ConvertFrom-Json
foreach ($file in $audit.files) {
    if ((Get-FileHash "$baselineRun/runtime/$($file.File)").Hash.ToLowerInvariant() -ne $file.SHA256) { throw 'Baseline changed after audit.' }
}
$changed = "$replacementRoot/prefix/bin/libplist-2.0.dll"
$original = $audit.files | Where-Object File -eq 'libplist-2.0.dll'
$modified = & "$PSScriptRoot/inspect-pe.ps1" -Path $changed | ConvertFrom-Json
if ($original.SHA256 -eq $modified.SHA256 -or $modified.Machine -ne '0x8664' -or $modified.CoffTimestamp -ne 0 -or
    $modified.DelayImports.Count -or $modified.Forwarders.Count -or
    (Compare-Object @($original.Imports) @($modified.Imports)) -or (Compare-Object @($original.Exports) @($modified.Exports))) {
    throw 'Expected an observably changed, compatible LGPL library with unchanged imports and exports.'
}
New-Item -ItemType Directory $trial | Out-Null
foreach ($kind in @('official','recipient')) {
    $sourceRoot = Join-Path $trial "$kind-source"
    New-Item -ItemType Directory $sourceRoot | Out-Null
    foreach ($file in Get-ChildItem "$repositoryRoot/src" -File -Recurse | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }) {
        $relative = $file.FullName.Substring($repositoryRoot.Length + 1)
        $target = Join-Path $sourceRoot $relative
        New-Item -ItemType Directory -Force (Split-Path -Parent $target) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
    foreach ($name in @('Directory.Build.props','NuGet.Config','LICENSE')) { Copy-Item -LiteralPath "$repositoryRoot/$name" -Destination $sourceRoot }
}
$pinPath = "$trial/recipient-source/src/Cardryft.Device/Apple/LibimobileDevice/HardenedRuntimeManifest.cs"
$pinText = Get-Content $pinPath -Raw
$oldLine = "new(`"libplist-2.0.dll`", $($original.Size), `"$($original.SHA256)`""
$newLine = "new(`"libplist-2.0.dll`", $($modified.Size), `"$($modified.SHA256)`""
if (-not $pinText.Contains($oldLine)) { throw 'Application compiled pins do not match the baseline.' }
[IO.File]::WriteAllText($pinPath,$pinText.Replace($oldLine,$newLine),[Text.UTF8Encoding]::new($false))
$modified | ConvertTo-Json -Depth 8 | Set-Content "$trial/modified-library-audit.json" -Encoding utf8
[ordered]@{
    modification='libplist_version() returns PACKAGE_VERSION "-recipient-rebuild"; unchanged ABI and limits.'
    sourceSha256=(Get-FileHash "$replacementRoot/src/libplist-2.7.0/src/plist.c").Hash.ToLowerInvariant()
    originalDllSha256=$original.SHA256; modifiedDllSha256=$modified.SHA256
    applicationChange='Only the libplist size/SHA-256 compiled pin changed in the recipient source copy.'
    officialPolicyBypassed=$false
} | ConvertTo-Json | Set-Content "$trial/recipient-modification.json" -Encoding utf8

function Invoke-OfflineChild([string]$Executable,[string[]]$Arguments,[string]$Log,[int]$Timeout) {
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName=$Executable; $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $start.WorkingDirectory=$trial
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    foreach ($argument in $Arguments) { $start.ArgumentList.Add($argument) }
    $environment = @{
        DOTNET_CLI_HOME="$repositoryRoot/.local/dotnet"; DOTNET_CLI_TELEMETRY_OPTOUT='1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'; DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE='true'; DOTNET_NOLOGO='true'
        NUGET_PACKAGES="$repositoryRoot/.local/nuget/packages"; NUGET_HTTP_CACHE_PATH="$repositoryRoot/.local/nuget/http-cache"
        NUGET_PLUGINS_CACHE_PATH="$repositoryRoot/.local/nuget/plugins-cache"
        APPDATA="$trial/state/roaming"; LOCALAPPDATA="$trial/state/local"; TEMP="$trial/state/tmp"; TMP="$trial/state/tmp"
        MSBUILDSDKREFERENCEDIRECTORY="$repositoryRoot/.local/sdk-references"; MSBUILDDISABLEREGISTRYFORSDKLOOKUP='1'
        PATH="$env:SystemRoot\System32"
    }
    foreach ($name in $environment.Keys) { $start.Environment[$name]=$environment[$name] }
    $start.Environment.Remove('USBMUXD_SOCKET_ADDRESS') | Out-Null
    New-Item -ItemType Directory -Force $environment.TEMP,$environment.APPDATA,$environment.LOCALAPPDATA | Out-Null
    $process=[Diagnostics.Process]::Start($start)
    try {
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        if (-not $process.WaitForExit($Timeout)) { $process.Kill($true); throw 'Offline child exceeded deadline.' }
        $output=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
        [IO.File]::WriteAllText($Log,$output,[Text.UTF8Encoding]::new($false))
        return $process.ExitCode
    }
    finally { $process.Dispose() }
}
$dotnet=(Get-Command dotnet -CommandType Application).Source
foreach ($kind in @('official','recipient')) {
    $output=Join-Path $trial "$kind-output"
    $project=Join-Path $trial "$kind-source/src/Cardryft.App/Cardryft.App.csproj"
    $exit=Invoke-OfflineChild $dotnet @('build',$project,'-c','Release','-p:CardryftNativeOfflineProbe=true','-o',$output) "$trial/$kind-build.log" 60000
    if ($exit -ne 0) { throw "Modified-application build failed: $kind ($exit). Inspect the local log." }
    New-Item -ItemType Directory "$output/native/win-x64" -Force | Out-Null
    foreach ($file in $audit.files) { Copy-Item -LiteralPath "$baselineRun/runtime/$($file.File)" -Destination "$output/native/win-x64" }
    if ($kind -eq 'recipient') { Copy-Item -LiteralPath $changed -Destination "$output/native/win-x64/libplist-2.0.dll" }
}
$wrong=Join-Path $trial 'wrong-output'
New-Item -ItemType Directory $wrong | Out-Null
Copy-Item -Path "$trial/official-output/*" -Destination $wrong -Recurse
Copy-Item -LiteralPath $changed -Destination "$wrong/native/win-x64/libplist-2.0.dll"
$results=foreach ($kind in @('official','wrong','recipient')) {
    $output=Join-Path $trial "$kind-output"
    $exit=Invoke-OfflineChild $dotnet @("$output/Cardryft.App.dll",'--native-offline-probe') "$trial/$kind-probe.log" 30000
    $expected=if ($kind -eq 'wrong') {1} else {0}
    $record="$output/offline-native-probe-result.json"
    if ($exit -ne $expected -or ((Test-Path $record) -ne ($kind -ne 'wrong'))) { throw "Offline application/loader probe failed for $kind ($exit)." }
    [ordered]@{kind=$kind;exitCode=$exit;expected=$expected;winFormsShown=(Test-Path $record);deviceOperations=0}
}
[ordered]@{baseline=$Baseline;replacement=$Replacement;original=$original.SHA256;modified=$modified.SHA256;results=@($results);officialHashPolicyUnchanged=$true;runtimePromoted=$false} |
    ConvertTo-Json -Depth 8 | Set-Content "$trial/results.json" -Encoding utf8
Get-Content "$trial/results.json"
