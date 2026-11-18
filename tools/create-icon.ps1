$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assetsDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'app\assets'
# Original asset copied from the locally installed Google Chrome VisualElements/Logo.png.
# Keep source colors and artwork; crop transparent padding and rotate clockwise by 120 degrees.
$source = [System.Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'icon-source\chrome-original.png'))
$master = [System.Drawing.Bitmap]::new(1024, 1024)
$graphics = [System.Drawing.Graphics]::FromImage($master)
try {
    $left=$source.Width; $top=$source.Height; $right=-1; $bottom=-1
    for($y=0; $y -lt $source.Height; $y++) {
        for($x=0; $x -lt $source.Width; $x++) {
            if($source.GetPixel($x,$y).A -gt 0) {
                $left=[Math]::Min($left,$x); $right=[Math]::Max($right,$x)
                $top=[Math]::Min($top,$y); $bottom=[Math]::Max($bottom,$y)
            }
        }
    }
    if($right -lt $left){throw '原始图标为空'}
    $crop=[System.Drawing.Rectangle]::new($left,$top,$right-$left+1,$bottom-$top+1)
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.TranslateTransform(512,512)
    $graphics.RotateTransform(120)
    $graphics.TranslateTransform(-512,-512)
    $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0,0,1024,1024), $crop, [System.Drawing.GraphicsUnit]::Pixel)
} finally { $graphics.Dispose(); $source.Dispose() }
try {
    $entries = @()
    foreach($size in @(16, 24, 32, 48, 64, 128, 256, 512)) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [System.IO.MemoryStream]::new()
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($master, 0, 0, $size, $size)
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            if($size -eq 512) { [System.IO.File]::WriteAllBytes((Join-Path $assetsDir 'pocketview.png'), $stream.ToArray()) }
            else { $entries += [pscustomobject]@{Size=$size; Bytes=$stream.ToArray()} }
        } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
    $file = [System.IO.File]::Create((Join-Path $assetsDir 'pocketview.ico'))
    $writer = [System.IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$entries.Count)
        $offset = 6 + 16 * $entries.Count
        foreach($entry in $entries) {
            $dimension = if($entry.Size -eq 256){0}else{$entry.Size}
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$entry.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $entry.Bytes.Length
        }
        foreach($entry in $entries) { $writer.Write([byte[]]$entry.Bytes) }
    } finally { $writer.Dispose(); $file.Dispose() }
} finally { $master.Dispose() }
$pngBase64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes((Join-Path $assetsDir 'pocketview.png')))
$svg = '<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256"><image width="256" height="256" href="data:image/png;base64,' + $pngBase64 + '"/></svg>'
[System.IO.File]::WriteAllText((Join-Path $assetsDir 'mark.svg'), $svg, [System.Text.UTF8Encoding]::new($false))
Write-Output '已生成圆形 SVG、PNG 和 7 种尺寸的透明 ICO。'
