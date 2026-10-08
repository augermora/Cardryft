param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Baseline,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Replacement,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$base = Join-Path $repositoryRoot '.local/native-build'
$run = Join-Path $base "runs/$Baseline"
$target = Join-Path $base "replacement-fixtures/$Label"
if (Test-Path $target) { throw 'Refusing to overwrite fixture evidence.' }
$sourceLock=Get-Content "$run/audit/sources-lock.json" -Raw | ConvertFrom-Json
foreach ($source in $sourceLock.localSources) {
    if ((Get-FileHash "$run/$($source.file)").Hash.ToLowerInvariant() -ne $source.sha256) { throw 'Baseline compiled fixture source changed.' }
}
$audit=Get-Content "$run/audit/pe-audit.json" -Raw | ConvertFrom-Json
New-Item -ItemType Directory $target | Out-Null
Copy-Item -LiteralPath "$run/shim/native-fixtures.exe" -Destination $target
foreach ($name in @('libcrypto-3-x64.dll','libssl-3-x64.dll')) {
    $pin=$audit.files | Where-Object File -eq $name
    if ((Get-FileHash "$run/runtime/$name").Hash.ToLowerInvariant() -ne $pin.SHA256) { throw 'Baseline dependency changed.' }
    Copy-Item -LiteralPath "$run/runtime/$name" -Destination $target
}
Copy-Item -LiteralPath "$base/replacements/$Replacement/prefix/bin/libplist-2.0.dll" -Destination $target
$start=[Diagnostics.ProcessStartInfo]::new()
$start.FileName="$target/native-fixtures.exe"; $start.WorkingDirectory=$target
$start.UseShellExecute=$false; $start.CreateNoWindow=$true
$start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
$start.Environment['PATH']="$env:SystemRoot\System32"
$process=[Diagnostics.Process]::Start($start)
try {
    $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
    if (-not $process.WaitForExit(30000)) { $process.Kill($true); throw 'Memory-only fixtures exceeded deadline.' }
    $output=$stdout.GetAwaiter().GetResult()+$stderr.GetAwaiter().GetResult()
    [IO.File]::WriteAllText("$target/results.txt",$output,[Text.UTF8Encoding]::new($false))
    if ($process.ExitCode -ne 0 -or $output -notmatch '43 passed, 0 failed') { throw 'Replacement changed offline parser/TLS behavior.' }
    [ordered]@{baseline=$Baseline;replacement=$Replacement;testsPassed=43;testsFailed=0;deviceOperations=0;modifiedDllSha256=(Get-FileHash "$target/libplist-2.0.dll").Hash.ToLowerInvariant()} |
        ConvertTo-Json | Set-Content "$target/results.json" -Encoding utf8
    Write-Output $output.Trim()
}
finally { $process.Dispose() }
