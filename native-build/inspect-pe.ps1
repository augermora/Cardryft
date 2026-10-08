param([Parameter(Mandatory)][string]$Path)
$ErrorActionPreference = 'Stop'
$peBytes = [IO.File]::ReadAllBytes((Resolve-Path -LiteralPath $Path).Path)
function Read-U16([int]$offset) { [BitConverter]::ToUInt16($peBytes, $offset) }
function Read-U32([int]$offset) { [BitConverter]::ToUInt32($peBytes, $offset) }
if ((Read-U16 0) -ne 0x5a4d) { throw 'Missing MZ signature.' }
$peOffset = Read-U32 0x3c
if ((Read-U32 $peOffset) -ne 0x4550) { throw 'Missing PE signature.' }
$machine = Read-U16 ($peOffset + 4)
$sectionCount = Read-U16 ($peOffset + 6)
if ($sectionCount -gt 96) { throw 'Unbounded section count.' }
$optionalSize = Read-U16 ($peOffset + 20)
$optional = $peOffset + 24
$magic = Read-U16 $optional
if ($magic -ne 0x20b) { throw 'Expected PE32+.' }
$directories = $optional + 112
$sections = @()
for ($index = 0; $index -lt $sectionCount; $index++) {
    $section = $optional + $optionalSize + 40 * $index
    $sections += [PSCustomObject]@{
        VirtualSize = Read-U32 ($section + 8)
        Rva = Read-U32 ($section + 12)
        RawSize = Read-U32 ($section + 16)
        Raw = Read-U32 ($section + 20)
    }
}
function Get-Offset([uint32]$rva) {
    if ($rva -lt (Read-U32 ($optional + 60))) { return [int]$rva }
    foreach ($section in $sections) {
        if ($rva -ge $section.Rva -and ($rva - $section.Rva) -lt $section.RawSize) {
            return [int]($section.Raw + $rva - $section.Rva)
        }
    }
    throw 'Unmapped RVA.'
}
function Read-Ascii([uint32]$rva) {
    $start = Get-Offset $rva
    $end = $start
    while ($end -lt $peBytes.Length -and $peBytes[$end] -ne 0 -and ($end - $start) -lt 4096) { $end++ }
    if ($end -ge $peBytes.Length -or $peBytes[$end] -ne 0) { throw 'Unterminated name.' }
    [Text.Encoding]::ASCII.GetString($peBytes, $start, $end - $start)
}
$imports = @()
$importRva = Read-U32 ($directories + 8)
if ($importRva) {
    $descriptor = Get-Offset $importRva
    for ($index = 0; $index -lt 1024; $index++) {
        $nameRva = Read-U32 ($descriptor + 12)
        if (-not $nameRva) { break }
        $imports += Read-Ascii $nameRva
        $descriptor += 20
    }
}
$delayed = @()
$delayRva = Read-U32 ($directories + 13 * 8)
if ($delayRva) {
    $descriptor = Get-Offset $delayRva
    for ($index = 0; $index -lt 1024; $index++) {
        $nameRva = Read-U32 ($descriptor + 4)
        if (-not $nameRva) { break }
        if ((Read-U32 $descriptor) -ne 1) { throw 'Unsupported delay-import addressing.' }
        $delayed += Read-Ascii $nameRva
        $descriptor += 32
    }
}
$exports = @()
$forwarders = @()
$functionCount = 0
$exportRva = Read-U32 $directories
if ($exportRva) {
    $export = Get-Offset $exportRva
    $functionCount = Read-U32 ($export + 20)
    $names = Read-U32 ($export + 24)
    if ($names -gt 100000) { throw 'Unbounded export count.' }
    $nameArray = Get-Offset (Read-U32 ($export + 32))
    for ($index = 0; $index -lt $names; $index++) { $exports += Read-Ascii (Read-U32 ($nameArray + $index * 4)) }
    if ($functionCount -gt 100000) { throw 'Unbounded function count.' }
    $functionArray = Get-Offset (Read-U32 ($export + 28))
    $exportSize = Read-U32 ($directories + 4)
    for ($index = 0; $index -lt $functionCount; $index++) {
        $functionRva = Read-U32 ($functionArray + $index * 4)
        if ($functionRva -ge $exportRva -and ($functionRva - $exportRva) -lt $exportSize) {
            $forwarders += Read-Ascii $functionRva
        }
    }
}
[PSCustomObject]@{
    File = [IO.Path]::GetFileName($Path)
    Size = $peBytes.Length
    CoffTimestamp = Read-U32 ($peOffset + 8)
    Machine = ('0x{0:x4}' -f $machine)
    OptionalHeader = ('0x{0:x4}' -f $magic)
    SHA256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
    Imports = $imports
    DelayImports = $delayed
    Exports = $exports
    ExportFunctionCount = $functionCount
    Forwarders = $forwarders
} | ConvertTo-Json -Depth 4
