param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$Label,
    [Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$target = [IO.Path]::GetFullPath($Destination)
if (-not $target.StartsWith($repositoryRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $target)) {
    throw 'Use a new file inside the repository. Review pins before adopting them in an explicitly rebuilt application.'
}
$run = Join-Path $repositoryRoot ".local/native-build/runs/$Label"
$audit = Get-Content "$run/audit/pe-audit.json" -Raw | ConvertFrom-Json
if ($audit.files.Count -ne 4) { throw 'Expected four audited candidate files.' }
$lines = [Collections.Generic.List[string]]::new()
$lines.Add('// Generated from a static build audit. This does not approve promotion or hardware use.')
$lines.Add('namespace Cardryft.Device.Apple.LibimobileDevice;')
$lines.Add('')
$lines.Add('internal static class HardenedRuntimeManifest')
$lines.Add('{')
$lines.Add('    internal const bool PromotionApproved = false;')
$lines.Add('    internal static IReadOnlyList<NativeFilePin> Pins { get; } = Array.AsReadOnly(new NativeFilePin[]')
$lines.Add('    {')
foreach ($file in $audit.files | Sort-Object File) {
    if ($file.File -notin @('cardryft-device.dll','libcrypto-3-x64.dll','libssl-3-x64.dll','libplist-2.0.dll') -or
        $file.SHA256 -notmatch '^[a-f0-9]{64}$' -or $file.Machine -ne '0x8664' -or $file.DelayImports.Count -or $file.Forwarders.Count -or
        (Get-FileHash "$run/runtime/$($file.File)").Hash.ToLowerInvariant() -ne $file.SHA256) { throw 'Invalid or changed candidate.' }
    foreach ($dependency in $file.Imports) { if ($dependency -notmatch '^[a-zA-Z0-9_.-]+\.dll$') { throw 'Invalid import name.' } }
    $imports = ($file.Imports | ForEach-Object { '"' + $_ + '"' }) -join ', '
    $lines.Add("        new(`"$($file.File)`", $($file.Size), `"$($file.SHA256)`", Array.AsReadOnly(new[] { $imports })),")
}
$lines.Add('    });')
$lines.Add('}')
[IO.File]::WriteAllText($target, ($lines -join "`n")+"`n", [Text.UTF8Encoding]::new($false))
Write-Output 'New compiled pin source generated. No runtime JSON override, promotion or hardware authorization.'
