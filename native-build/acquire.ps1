$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repositoryRoot '.local/native-build'
$downloadRoot = Join-Path $localRoot 'downloads'
New-Item -ItemType Directory -Force -Path $downloadRoot | Out-Null
$toolLock = Get-Content "$PSScriptRoot/toolchain-lock.json" -Raw | ConvertFrom-Json
$sourceLock = Get-Content "$PSScriptRoot/sources-lock.json" -Raw | ConvertFrom-Json
foreach ($item in @($toolLock.base) + @($toolLock.packages) + @($toolLock.correspondingSource) + @($sourceLock.sources)) {
    $target = Join-Path $downloadRoot $item.file
    if (-not (Test-Path -LiteralPath $target)) {
        Invoke-WebRequest -Uri $item.url -OutFile $target -UseBasicParsing
    }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $item.sha256) {
        throw "SHA-256 mismatch: $($item.file). Nothing is extracted or executed."
    }
    Write-Output "Verified $($item.file)"
}
