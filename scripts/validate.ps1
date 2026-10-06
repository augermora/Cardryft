$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$localRoot = Join-Path $repositoryRoot '.local'
$environment = @{
    DOTNET_CLI_HOME = Join-Path $localRoot 'dotnet'
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
    DOTNET_NOLOGO = 'true'
    NUGET_PACKAGES = Join-Path $localRoot 'nuget/packages'
    NUGET_HTTP_CACHE_PATH = Join-Path $localRoot 'nuget/http-cache'
    NUGET_PLUGINS_CACHE_PATH = Join-Path $localRoot 'nuget/plugins-cache'
    APPDATA = Join-Path $localRoot 'appdata/roaming'
    LOCALAPPDATA = Join-Path $localRoot 'appdata/local'
    MSBUILDSDKREFERENCEDIRECTORY = Join-Path $localRoot 'sdk-references'
    MSBUILDDISABLEREGISTRYFORSDKLOOKUP = '1'
    TEMP = Join-Path $localRoot 'tmp'
    TMP = Join-Path $localRoot 'tmp'
}
$originalEnvironment = @{}

try {
    foreach ($name in $environment.Keys) {
        $originalEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $environment[$name], 'Process')
    }
    New-Item -ItemType Directory -Force -Path $environment.TEMP | Out-Null
    New-Item -ItemType Directory -Force -Path $environment.MSBUILDSDKREFERENCEDIRECTORY | Out-Null
    Push-Location -LiteralPath $repositoryRoot
    try {
        dotnet restore
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed ($LASTEXITCODE)." }

        dotnet build -c Release
        if ($LASTEXITCODE -ne 0) { throw "dotnet build failed ($LASTEXITCODE)." }

        dotnet test -c Release
        if ($LASTEXITCODE -ne 0) { throw "dotnet test failed ($LASTEXITCODE)." }
    }
    finally {
        Pop-Location
    }
}
finally {
    foreach ($name in $originalEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $originalEnvironment[$name], 'Process')
    }
}
