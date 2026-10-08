param([Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runRoot = Join-Path $repositoryRoot ".local/native-build/runs/$Label"
$runtimeRoot = Join-Path $runRoot 'runtime'
$sourceLock = Get-Content "$runRoot/audit/sources-lock.json" -Raw | ConvertFrom-Json
$mapping = @{
    'libcrypto-3-x64.dll'='openssl'; 'libssl-3-x64.dll'='openssl'
    'libplist-2.0.dll'='libplist'; 'cardryft-device.dll'='cardryft-shim'
}
$nativeEdges = @{
    'libcrypto-3-x64.dll'=@(); 'libplist-2.0.dll'=@()
    'libssl-3-x64.dll'=@('libcrypto-3-x64.dll')
    'cardryft-device.dll'=@('libcrypto-3-x64.dll','libssl-3-x64.dll','libplist-2.0.dll')
}
$systemNames = @('ADVAPI32.dll','BCRYPT.dll','CRYPT32.dll','GDI32.dll','IPHLPAPI.dll','KERNEL32.dll','ole32.dll','SHELL32.dll','USER32.dll','WS2_32.dll')
$crtNames = @('convert','environment','filesystem','heap','locale','math','private','process','runtime','stdio','string','time','utility') | ForEach-Object {"api-ms-win-crt-$_-l1-1-0.dll"}
$files = @(Get-ChildItem -LiteralPath $runtimeRoot -File)
if (Test-Path "$runRoot/audit/pe-audit.json") { throw 'Refusing to overwrite existing audit evidence.' }
if ($files.Count -ne $mapping.Count) { throw 'Missing or extra runtime artifacts.' }
$reports = foreach ($file in $files | Sort-Object Name) {
    if (-not $mapping.ContainsKey($file.Name)) { throw "Unexpected file: $($file.Name)" }
    $report = & "$PSScriptRoot/inspect-pe.ps1" -Path $file.FullName | ConvertFrom-Json
    if ($report.Machine -ne '0x8664' -or $report.OptionalHeader -ne '0x020b') { throw 'Expected AMD64 PE32+.' }
    if ($report.CoffTimestamp -ne 0) { throw 'Non-deterministic PE timestamp.' }
    if ($report.DelayImports.Count -ne 0) { throw 'Unexpected delay imports.' }
    if ($report.Forwarders.Count -ne 0) { throw 'Unexpected forwarded exports.' }
    $actualNative = @($report.Imports | Where-Object { $mapping.ContainsKey($_) } | Sort-Object)
    $expectedNative = @($nativeEdges[$file.Name] | Sort-Object)
    if (($actualNative -join ',') -ne ($expectedNative -join ',')) { throw "Unexpected native dependency graph for $($file.Name)." }
    foreach ($dependency in @($report.Imports) + @($report.DelayImports)) {
        if ($mapping.ContainsKey($dependency)) {
            if (-not (Test-Path -LiteralPath (Join-Path $runtimeRoot $dependency))) { throw "Missing dependency $dependency" }
        }
        elseif ($dependency -notin $systemNames -and $dependency -notin $crtNames) { throw "Unexpected dependency: $dependency" }
    }
    if ($file.Name -eq 'cardryft-device.dll') {
        $expected = @(Get-Content "$repositoryRoot/.local/native-build/recipes/$Label/shim/cardryft.exports" | Where-Object {$_ -ne ''})
        if (Compare-Object $expected @($report.Exports)) { throw 'Root export surface differs from reviewed list.' }
        if ($report.ExportFunctionCount -ne $expected.Count) { throw 'Unexpected ordinal-only root exports.' }
    }
    $source = $sourceLock.sources | Where-Object name -eq $mapping[$file.Name]
    if ($file.Name -eq 'cardryft-device.dll') { $source=[PSCustomObject]@{name='cardryft-shim';version='1';license='MIT';files=$sourceLock.localSources} }
    $report | Add-Member -NotePropertyName Source -NotePropertyValue $source
    $report | Add-Member -NotePropertyName Patches -NotePropertyValue @($sourceLock.patches | Where-Object project -eq $source.name)
    $report | Add-Member -NotePropertyName LicenseMapping -NotePropertyValue @{
        upstream=$source.license
        combinedSelection=$(if ($source.name -eq 'openssl') {'Apache-2.0'} elseif($source.name -eq 'cardryft-shim') {'MIT application using LGPL-3.0 source/recombination route'} else {'LGPL-3.0 via or-later permission, plus preserved file-specific notices'})
        compilerRuntime='GCC 16.2.0-4 GPL-3.0-or-later WITH GCC-exception-3.1'
        crtHeaders='MinGW 14.0.0.r426.g4564ee4b5-1 ZPL-2.1 and file-specific permissive terms'
        notices='THIRD-PARTY-NOTICES.md and matching source/release-material'
    }
    $report
}
$output = [ordered]@{schemaVersion=1;purpose='Build audit only; NOT a production loader manifest';runtimeApproved=$false;files=@($reports)}
[IO.File]::WriteAllText("$runRoot/audit/pe-audit.json", ($output | ConvertTo-Json -Depth 12)+"`n", [Text.UTF8Encoding]::new($false))
$reports | Select-Object File,Size,SHA256
Write-Output 'Static closure passes. Dynamic loading, licensing and interop review remain separate gates.'
