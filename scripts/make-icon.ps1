# 把一张角色图转成多尺寸 .ico。
#
#   .\make-icon.ps1 -Source roles\鲸鱼娘.png             生成 assets\app.ico
#   .\make-icon.ps1 -Source roles\鲸鱼娘.png -Out out.ico
#
# 生成后重新运行 build.cmd 即可把图标嵌入 exe。
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$Out = ''
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
if (-not (Test-Path -LiteralPath $Source)) { throw "找不到源图片: $Source" }
if ([string]::IsNullOrEmpty($Out)) { $Out = Join-Path $root 'assets\app.ico' }
$outDir = Split-Path -Parent $Out
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }

$image = [System.Drawing.Image]::FromFile((Resolve-Path -LiteralPath $Source).Path)
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = New-Object System.Collections.ArrayList
foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap $size, $size
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $scale = [Math]::Min($size / $image.Width, $size / $image.Height)
    $width = [int][Math]::Max(1, [Math]::Round($image.Width * $scale))
    $height = [int][Math]::Max(1, [Math]::Round($image.Height * $scale))
    $graphics.DrawImage($image, [int](($size - $width) / 2), [int](($size - $height) / 2), $width, $height)
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$pngs.Add($stream.ToArray())
    $graphics.Dispose()
    $bitmap.Dispose()
}
$image.Dispose()

$file = [System.IO.File]::Create($Out)
$writer = New-Object System.IO.BinaryWriter $file
$writer.Write([int16]0)
$writer.Write([int16]1)
$writer.Write([int16]$pngs.Count)
$offset = 6 + 16 * $pngs.Count
for ($i = 0; $i -lt $pngs.Count; $i++) {
    $size = $sizes[$i]
    $dim = if ($size -ge 256) { 0 } else { $size }
    $writer.Write([byte]$dim)
    $writer.Write([byte]$dim)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([int16]1)
    $writer.Write([int16]32)
    $writer.Write([int]$pngs[$i].Length)
    $writer.Write([int]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $writer.Write($png) }
$writer.Dispose()
$file.Dispose()
Write-Host "图标已生成: $Out（重新运行 build.cmd 生效）"
