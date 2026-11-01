param([switch]$Publish)
. (Join-Path $PSScriptRoot 'bootstrap.ps1')
Set-Location $projectRoot
& $env:EXPERIMENT_DOTNET build src\Experiments.App\Experiments.App.csproj -c Release --nologo
if($LASTEXITCODE){throw 'App build failed'}
& $env:EXPERIMENT_DOTNET build src\Experiments.Cli\Experiments.Cli.csproj -c Release --nologo
if($LASTEXITCODE){throw 'CLI build failed'}
if($Publish){
 $destination=Join-Path $projectRoot 'releases\LM-Experiment-Workbench-win-x64'
 & $env:EXPERIMENT_DOTNET publish src\Experiments.App\Experiments.App.csproj -c Release -r win-x64 --self-contained true -o $destination --nologo
 if($LASTEXITCODE){throw 'App publish failed'}
 & $env:EXPERIMENT_DOTNET publish src\Experiments.Cli\Experiments.Cli.csproj -c Release -r win-x64 --self-contained true -o $destination --nologo
 if($LASTEXITCODE){throw 'CLI publish failed'}
 Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $destination 'LICENSE.txt')
 Copy-Item -LiteralPath (Join-Path $projectRoot 'NOTICE.md') -Destination (Join-Path $destination 'WORKBENCH-NOTICE.md')
 Copy-Item -LiteralPath (Join-Path $projectRoot 'licenses\DOTNET-LICENSE.txt') -Destination (Join-Path $destination 'DOTNET-LICENSE.txt')
 Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $destination 'README.md')
}
