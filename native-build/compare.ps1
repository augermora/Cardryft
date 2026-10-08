param(
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$BuildA,
    [Parameter(Mandatory)][ValidatePattern('^[A-Za-z0-9_-]+$')][string]$BuildB)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repositoryRoot '.local/native-build/runs'
$recipes = Join-Path $repositoryRoot '.local/native-build/recipes'
if (Test-Path "$root/$BuildB/audit/comparison.json") { throw 'Refusing to overwrite existing comparison evidence.' }
if ((Get-FileHash "$recipes/$BuildA/build-inputs.json").Hash -ne (Get-FileHash "$recipes/$BuildB/build-inputs.json").Hash) { throw 'Builds did not use identical locked recipes/inputs.' }
$first = (Get-Content "$root/$BuildA/audit/pe-audit.json" -Raw | ConvertFrom-Json).files
$second = (Get-Content "$root/$BuildB/audit/pe-audit.json" -Raw | ConvertFrom-Json).files
if (Compare-Object @($first.File) @($second.File)) { throw 'Different output file sets.' }
$results = foreach ($a in $first) {
    $b = $second | Where-Object File -eq $a.File
    $bytesA = [IO.File]::ReadAllBytes("$root/$BuildA/runtime/$($a.File)")
    $bytesB = [IO.File]::ReadAllBytes("$root/$BuildB/runtime/$($a.File)")
    if ((Get-FileHash "$root/$BuildA/runtime/$($a.File)").Hash.ToLowerInvariant() -ne $a.SHA256 -or
        (Get-FileHash "$root/$BuildB/runtime/$($b.File)").Hash.ToLowerInvariant() -ne $b.SHA256) { throw 'Output changed after audit.' }
    $same = [Collections.StructuralComparisons]::StructuralEqualityComparer.Equals($bytesA, $bytesB)
    $firstDifference = $null
    if (-not $same) {
        for ($i = 0; $i -lt [Math]::Min($bytesA.Length,$bytesB.Length); $i++) {
            if ($bytesA[$i] -ne $bytesB[$i]) { $firstDifference=$i; break }
        }
    }
    [PSCustomObject]@{file=$a.File;identical=$same;sha256A=$a.SHA256;sha256B=$b.SHA256;firstDifference=$firstDifference}
}
$output = [ordered]@{buildA=$BuildA;buildB=$BuildB;files=@($results)}
[IO.File]::WriteAllText("$root/$BuildB/audit/comparison.json", ($output | ConvertTo-Json -Depth 6)+"`n", [Text.UTF8Encoding]::new($false))
$results | Format-Table file,identical,sha256A -AutoSize
if ($results.identical -contains $false) { throw 'Reproducibility gate failed. Inspect differences; do not promote.' }
