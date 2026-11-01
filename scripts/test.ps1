. (Join-Path $PSScriptRoot 'bootstrap.ps1')
Set-Location $projectRoot
$output=Join-Path $projectRoot ('fixtures\run-'+[Guid]::NewGuid().ToString('N'))
& $env:EXPERIMENT_DOTNET run --project src\Experiments.Cli\Experiments.Cli.csproj -c Release -- --self-test (Join-Path $output 'core')
if($LASTEXITCODE){throw 'Core/HTTP tests failed'}
& $env:EXPERIMENT_DOTNET (Join-Path $projectRoot 'src\Experiments.App\bin\Release\net9.0-windows\experiment-workbench.dll') --self-test (Join-Path $output 'ui')
if($LASTEXITCODE){throw 'Native UI tests failed'}
Write-Output "Verification outputs: $output"
