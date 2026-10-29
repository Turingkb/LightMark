param([Parameter(Mandatory = $true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$images = @()
foreach ($size in @(16, 32, 48, 256)) {
    $bitmap = [Drawing.Bitmap]::new($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform($size / 256.0, $size / 256.0)
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    foreach ($arc in @(@(8,8,52,52,180), @(196,8,52,52,270), @(196,196,52,52,0), @(8,196,52,52,90))) { $path.AddArc($arc[0],$arc[1],$arc[2],$arc[3],$arc[4],90) }
    $path.CloseFigure()
    $brush = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#28766D'))
    $graphics.FillPath($brush, $path)
    $pen = [Drawing.Pen]::new([Drawing.Color]::White, 18)
    $pen.LineJoin = [Drawing.Drawing2D.LineJoin]::Round
    $graphics.DrawLines($pen, [Drawing.PointF[]]@([Drawing.PointF]::new(47,177),[Drawing.PointF]::new(47,79),[Drawing.PointF]::new(91,135),[Drawing.PointF]::new(135,79),[Drawing.PointF]::new(135,177)))
    $graphics.DrawLine($pen, 193, 79, 193, 172)
    $graphics.DrawLines($pen, [Drawing.PointF[]]@([Drawing.PointF]::new(166,149),[Drawing.PointF]::new(193,177),[Drawing.PointF]::new(220,149)))
    $memory = [IO.MemoryStream]::new()
    $bitmap.Save($memory, [Drawing.Imaging.ImageFormat]::Png)
    $images += [PSCustomObject]@{Size=$size;Bytes=$memory.ToArray()}
    $memory.Dispose(); $pen.Dispose(); $brush.Dispose(); $path.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
$output = [IO.File]::Create($OutputPath)
$writer = [IO.BinaryWriter]::new($output)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + $images.Count * 16
    foreach ($image in $images) {
        $dimension = if ($image.Size -eq 256) { 0 } else { $image.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$image.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $image.Bytes.Length
    }
    foreach ($image in $images) { $writer.Write([byte[]]$image.Bytes) }
}
finally { $writer.Dispose(); $output.Dispose() }
