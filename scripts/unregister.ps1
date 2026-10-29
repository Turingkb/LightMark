$ErrorActionPreference = 'Stop'
# Remove only LightMark's application registration; Windows' UserChoice is never changed.
foreach ($extension in @('.md', '.markdown', '.mdown', '.mkd')) {
    $associationKey = "HKCU:\Software\Classes\$extension\OpenWithProgids"
    if (Test-Path -LiteralPath $associationKey) { Remove-ItemProperty -LiteralPath $associationKey -Name 'LightMark.Markdown' -ErrorAction SilentlyContinue }
}
foreach ($registration in @('HKCU:\Software\Classes\LightMark.Markdown', 'HKCU:\Software\Classes\Applications\LightMark.exe', 'HKCU:\Software\LightMark\Capabilities')) {
    if (Test-Path -LiteralPath $registration) { Remove-Item -LiteralPath $registration -Recurse }
}
if (Test-Path -LiteralPath 'HKCU:\Software\RegisteredApplications') { Remove-ItemProperty -LiteralPath 'HKCU:\Software\RegisteredApplications' -Name 'LightMark' -ErrorAction SilentlyContinue }
Write-Output '已移除 LightMark 的打开方式注册。请在 Windows 默认应用中选择替代编辑器。'
