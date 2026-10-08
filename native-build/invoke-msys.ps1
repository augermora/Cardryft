function Invoke-CardryftMsys {
    param([string]$ToolRoot, [string]$Script, [string[]]$ScriptArguments, [string]$StateRoot)
    $ErrorActionPreference = 'Stop'
    foreach ($path in @($ToolRoot, $Script, $StateRoot)) {
        $full = [IO.Path]::GetFullPath($path)
        $repo = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)) + [IO.Path]::DirectorySeparatorChar
        if (-not $full.StartsWith($repo, [StringComparison]::OrdinalIgnoreCase)) { throw 'Build path escaped repository.' }
    }
    New-Item -ItemType Directory -Force -Path "$StateRoot/home", "$StateRoot/tmp", "$StateRoot/appdata/local", "$StateRoot/appdata/roaming" | Out-Null
    $start = New-Object Diagnostics.ProcessStartInfo
    $start.FileName = Join-Path $ToolRoot 'usr/bin/bash.exe'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.WorkingDirectory = Split-Path -Parent $PSScriptRoot
    # Quoting is deliberately restricted to repository paths and fixed build labels.
    $args = @('--noprofile','--norc', ($Script -replace '\\','/')) + $ScriptArguments
    foreach ($arg in $args) { if ($arg -match '["\r\n]') { throw 'Unsupported argument.' } }
    $start.Arguments = ($args | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $start.EnvironmentVariables.Clear()
    $sourceLock = Get-Content "$PSScriptRoot/sources-lock.json" -Raw | ConvertFrom-Json
    $environment = @{
        SystemRoot=$env:SystemRoot; WINDIR=$env:SystemRoot; COMSPEC="$env:SystemRoot\System32\cmd.exe"
        PATH="$ToolRoot\ucrt64\bin;$ToolRoot\usr\bin;$env:SystemRoot\System32"
        HOME="$StateRoot/home"; USERPROFILE="$StateRoot/home"
        APPDATA="$StateRoot/appdata/roaming"; LOCALAPPDATA="$StateRoot/appdata/local"
        TEMP="$StateRoot/tmp"; TMP="$StateRoot/tmp"
        MSYSTEM='UCRT64'; MSYS2_PATH_TYPE='strict'; CHERE_INVOKING='1'
        LANG='C'; LC_ALL='C'; TZ='UTC'; SOURCE_DATE_EPOCH=[string]$sourceLock.sourceDateEpoch
    }
    foreach ($name in $environment.Keys) { $start.EnvironmentVariables[$name] = $environment[$name] }
    $process = [Diagnostics.Process]::Start($start)
    $process.StandardInput.Close()
    try {
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Controlled MSYS command failed ($($process.ExitCode)). Inspect build log." }
    }
    finally { $process.Dispose() }
}
