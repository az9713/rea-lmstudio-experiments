. (Join-Path $PSScriptRoot 'local-env.ps1')
if($env:EXPERIMENT_DOTNET -and (Test-Path -LiteralPath $env:EXPERIMENT_DOTNET)){return}
$env:EXPERIMENT_DOTNET=Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if(Test-Path -LiteralPath $env:EXPERIMENT_DOTNET){return}
$tools=Join-Path $projectRoot '.tools';New-Item -ItemType Directory -Force $tools | Out-Null
$metadata=Invoke-RestMethod 'https://builds.dotnet.microsoft.com/dotnet/release-metadata/9.0/releases.json'
$release=$metadata.releases | Where-Object {$_.sdk.version -eq '9.0.320'} | Select-Object -First 1
$file=$release.sdk.files | Where-Object {$_.rid -eq 'win-x64' -and $_.name -like '*.zip'} | Select-Object -First 1
if(-not $file){throw 'Pinned SDK metadata missing'}
$zip=Join-Path $tools 'sdk.zip';Invoke-WebRequest $file.url -OutFile $zip
if((Get-FileHash -LiteralPath $zip -Algorithm SHA512).Hash -ne $file.hash){throw 'SDK SHA512 mismatch'}
Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $tools 'dotnet')
& $env:EXPERIMENT_DOTNET --version
if($LASTEXITCODE){throw 'SDK failed'}
