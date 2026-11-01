$ErrorActionPreference='Stop'
$projectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$cacheRoot=Join-Path $projectRoot '.runtime'
foreach($name in @('tmp','dotnet-home','nuget','pycache')) {New-Item -ItemType Directory -Force (Join-Path $cacheRoot $name) | Out-Null}
$env:TEMP=$env:TMP=$env:TMPDIR=Join-Path $cacheRoot 'tmp'
$env:DOTNET_CLI_HOME=Join-Path $cacheRoot 'dotnet-home'
$env:NUGET_PACKAGES=Join-Path $cacheRoot 'nuget'
$env:PYTHONPYCACHEPREFIX=Join-Path $cacheRoot 'pycache'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH='false'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_NOLOGO='true'
