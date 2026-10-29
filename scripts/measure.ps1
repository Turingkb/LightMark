$ErrorActionPreference = 'Stop'
$projectDir = Split-Path $PSScriptRoot -Parent
$measurementDir = Join-Path $projectDir 'build\measurement'
New-Item -ItemType Directory -Path $measurementDir -Force | Out-Null
$fixture = Join-Path $measurementDir '中文 启动样本.md'
$sampleText = Get-Content (Join-Path $projectDir 'Welcome.md') -Raw
[IO.File]::WriteAllText($fixture, $sampleText, [Text.UTF8Encoding]::new($false))
$results = @()
foreach ($run in 1..3) {
    $startInfo = [Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = Join-Path $projectDir 'dist\LightMark-1.0.2\LightMark.exe'
    $startInfo.Arguments = '"' + $fixture + '"'
    $startInfo.UseShellExecute = $false
    $startInfo.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
    $startInfo.EnvironmentVariables['LIGHTMARK_DATA_DIR'] = Join-Path $measurementDir 'state'
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $measuredProcess = [Diagnostics.Process]::Start($startInfo)
    try {
        $idle = $measuredProcess.WaitForInputIdle(10000)
        $inputIdleMs = $watch.ElapsedMilliseconds
        do { $measuredProcess.Refresh(); if ($measuredProcess.HasExited) { throw '测量进程提前退出' }; if ($measuredProcess.MainWindowTitle.Length -gt 0) { break }; Start-Sleep -Milliseconds 10 } while ($watch.ElapsedMilliseconds -lt 10000)
        if ($measuredProcess.MainWindowTitle.Length -eq 0) { throw '窗口未在 10 秒内出现' }
        $windowMs = $watch.ElapsedMilliseconds
        Start-Sleep -Milliseconds 1200
        $measuredProcess.Refresh()
        $results += [PSCustomObject]@{run = $run; inputIdleMs = $inputIdleMs; firstTitledWindowMs = $windowMs; workingSetMiBAfter1200ms = [math]::Round($measuredProcess.WorkingSet64 / 1MB, 1); privateMiB = [math]::Round($measuredProcess.PrivateMemorySize64 / 1MB, 1); fileBytes = (Get-Item $fixture).Length; title = $measuredProcess.MainWindowTitle}
    }
    finally {
        if (-not $measuredProcess.HasExited) { $measuredProcess.CloseMainWindow() | Out-Null; if (-not $measuredProcess.WaitForExit(3000)) { $measuredProcess.Kill() } }
        $measuredProcess.Dispose()
    }
}
$results | ConvertTo-Json | Set-Content (Join-Path $measurementDir 'results.json') -Encoding utf8
$results | Format-Table
