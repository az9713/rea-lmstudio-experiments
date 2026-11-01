. (Join-Path $PSScriptRoot 'local-env.ps1')
Set-Location $projectRoot
python (Join-Path $PSScriptRoot 'package.py')
if($LASTEXITCODE){throw 'Packaging failed'}
