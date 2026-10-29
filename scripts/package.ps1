$ErrorActionPreference = 'Stop'
$projectDir = Split-Path $PSScriptRoot -Parent
$releaseDir = Join-Path $projectDir 'build\release'
New-Item -ItemType Directory -Path $releaseDir -Force | Out-Null
$zipPath = Join-Path $releaseDir 'LightMark-1.0.1-portable.zip'
Compress-Archive -Path (Join-Path $projectDir 'dist\LightMark-1.0.1\*') -DestinationPath $zipPath -Force
$hash = (Get-FileHash -LiteralPath $zipPath -Algorithm SHA256).Hash
[IO.File]::WriteAllText($zipPath + '.sha256', $hash + '  LightMark-1.0.1-portable.zip' + [Environment]::NewLine)
Write-Output $zipPath
Write-Output "SHA256 $hash"
