$ErrorActionPreference = 'Stop'
$projectDir = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build.ps1') -Offline
$testDir = Join-Path $projectDir 'build\tests'
New-Item -ItemType Directory -Path $testDir -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$frameworkDir = Split-Path $compiler -Parent
$references = @('System.dll', 'System.Core.dll', 'System.Xml.dll', 'System.Xml.Linq.dll', 'System.Xaml.dll')
foreach ($name in @('WindowsBase.dll', 'PresentationCore.dll', 'PresentationFramework.dll')) { $references += Join-Path $frameworkDir "WPF\$name" }
$packages = Get-Content (Join-Path $projectDir 'dependencies.json') -Raw | ConvertFrom-Json
$embedded = @()
foreach ($package in $packages) {
    $dll = Join-Path $projectDir ('.cache\' + $package.cache + '\lib\net462\' + $package.assembly)
    $references += $dll
    $embedded += '/resource:' + $dll + ',Dependencies.' + $package.assembly
    $oldCopy = Join-Path $testDir $package.assembly
    if (Test-Path -LiteralPath $oldCopy) { Remove-Item -LiteralPath $oldCopy }
}
$arguments = @('/nologo', '/target:exe', '/main:LightMark.Tests', '/warn:4', '/warnaserror+', '/utf8output', '/codepage:65001', ('/out:' + (Join-Path $testDir 'Tests.exe')))
$arguments += $references | ForEach-Object { '/reference:' + $_ }
$arguments += $embedded
$arguments += '/resource:' + (Join-Path $projectDir 'src\MainWindow.xaml') + ',LightMark.MainWindow.xaml'
$arguments += '/resource:' + (Join-Path $projectDir 'Welcome.md') + ',LightMark.Welcome.md'
$arguments += '/resource:' + (Join-Path $projectDir 'THIRD-PARTY-NOTICES.md') + ',LightMark.ThirdParty.txt'
$arguments += Get-ChildItem (Join-Path $projectDir 'src') -Filter '*.cs' | ForEach-Object { $_.FullName }
$arguments += Join-Path $projectDir 'tests\Tests.cs'
& $compiler $arguments
if ($LASTEXITCODE -ne 0) { throw '测试程序编译失败' }
& (Join-Path $testDir 'Tests.exe')
if ($LASTEXITCODE -ne 0) { throw '测试失败' }
