param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$projectDir = Split-Path $PSScriptRoot -Parent
$cacheDir = Join-Path $projectDir '.cache'
$outputDir = Join-Path $projectDir 'app'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$frameworkDir = Split-Path $compiler -Parent
New-Item -ItemType Directory -Path $cacheDir, $outputDir -Force | Out-Null
$buildDir = Join-Path $projectDir 'build'
New-Item -ItemType Directory -Path $buildDir -Force | Out-Null
$iconPath = Join-Path $buildDir 'LightMark.ico'
& (Join-Path $PSScriptRoot 'create-icon.ps1') -OutputPath $iconPath
$packages = Get-Content (Join-Path $projectDir 'dependencies.json') -Raw | ConvertFrom-Json
$references = @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Xml.Linq.dll', 'System.Xaml.dll')
foreach ($name in @('WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll')) { $references += Join-Path $frameworkDir "WPF\$name" }
$embedded = @()
foreach ($package in $packages) {
    $archive = Join-Path $cacheDir ($package.cache + '.zip')
    if (-not (Test-Path -LiteralPath $archive)) {
        if ($Offline) { throw "离线构建缺少依赖：$($package.id)" }
        $packageId = $package.id.ToLowerInvariant()
        $url = "https://api.nuget.org/v3-flatcontainer/$packageId/$($package.version)/$packageId.$($package.version).nupkg"
        Invoke-WebRequest -Uri $url -OutFile $archive
    }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $package.sha256) { throw "依赖校验失败：$($package.id)" }
    $expanded = Join-Path $cacheDir $package.cache
    Expand-Archive -LiteralPath $archive -DestinationPath $expanded -Force
    $dll = Join-Path $expanded ('lib\net462\' + $package.assembly)
    $references += $dll
    $embedded += '/resource:' + $dll + ',Dependencies.' + $package.assembly
}
$arguments = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/warn:4', '/warnaserror+', '/utf8output', '/codepage:65001', ('/out:' + (Join-Path $outputDir 'LightMark.exe')), ('/win32manifest:' + (Join-Path $projectDir 'src\app.manifest')))
$arguments += $references | ForEach-Object { '/reference:' + $_ }
$arguments += $embedded
$arguments += '/win32icon:' + $iconPath
$arguments += '/resource:' + (Join-Path $projectDir 'src\MainWindow.xaml') + ',LightMark.MainWindow.xaml'
$arguments += '/resource:' + (Join-Path $projectDir 'Welcome.md') + ',LightMark.Welcome.md'
$arguments += '/resource:' + (Join-Path $projectDir 'THIRD-PARTY-NOTICES.md') + ',LightMark.ThirdParty.txt'
$arguments += Get-ChildItem (Join-Path $projectDir 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Copy-Item (Join-Path $projectDir 'README.md'), (Join-Path $projectDir 'THIRD-PARTY-NOTICES.md'), (Join-Path $projectDir 'VALIDATION.md') -Destination $outputDir
Copy-Item (Join-Path $projectDir 'scripts\unregister.ps1') -Destination $outputDir
$commit = & git -C $projectDir rev-parse --short HEAD 2>$null
if ($LASTEXITCODE -ne 0) { $commit = 'uncommitted' }
[PSCustomObject]@{ version = '1.0.2'; sourceCommit = $commit; sha256 = (Get-FileHash (Join-Path $outputDir 'LightMark.exe') -Algorithm SHA256).Hash; builtAtUtc = [DateTime]::UtcNow.ToString('o') } | ConvertTo-Json | Set-Content (Join-Path $outputDir 'build-info.json') -Encoding utf8
Write-Output "已构建：$outputDir\LightMark.exe"
