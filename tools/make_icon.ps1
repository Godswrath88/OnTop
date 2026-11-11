param([string]$OutIco, [string]$PreviewPng)
# Draws a generic push-pin (thumbtack) from simple shapes and writes a
# multi-size .ico: 16-64 px as 32-bit BMP frames (loads everywhere), 256 px as PNG.
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'

function New-Pin([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)
    # Design space is 256x256; small sizes use bolder outlines so they stay readable.
    $s = $size / 256.0
    $g.ScaleTransform($s, $s)
    $g.TranslateTransform(128, 128)
    $g.RotateTransform(40)          # tip points to the lower left
    $g.ScaleTransform(1.18, 1.18)     # fill the frame (tray icons are 16 px)
    $g.TranslateTransform(-128, -112)
    $ow = if ($size -le 20) { 14 } elseif ($size -le 32) { 10 } elseif ($size -le 48) { 7 } else { 5 }
    $outline = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 70, 12, 12)), $ow
    $outline.LineJoin = 'Round'
    $metalPen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 45, 50, 58)), $ow

    # Needle: tapered steel spike.
    $needle = New-Object System.Drawing.Drawing2D.GraphicsPath
    $needle.AddPolygon([System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF 119, 150), (New-Object System.Drawing.PointF 137, 150),
        (New-Object System.Drawing.PointF 130, 244), (New-Object System.Drawing.PointF 126, 244)))
    $steel = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 119, 0), (New-Object System.Drawing.PointF 137, 0), ([System.Drawing.Color]::FromArgb(255, 235, 238, 242)), ([System.Drawing.Color]::FromArgb(255, 120, 128, 140))
    $g.FillPath($steel, $needle); $g.DrawPath($metalPen, $needle)

    $red    = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.PointF 70, 0), (New-Object System.Drawing.PointF 186, 0), ([System.Drawing.Color]::FromArgb(255, 255, 110, 100)), ([System.Drawing.Color]::FromArgb(255, 190, 30, 30))
    $redDk  = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 170, 25, 25))

    # Base flange (wide disc where the pin meets the board).
    $g.FillEllipse($redDk, 66, 128, 124, 34); $g.DrawEllipse($outline, 66, 128, 124, 34)
    # Waist: narrow body between flange and head.
    $body = New-Object System.Drawing.Drawing2D.GraphicsPath
    $body.AddPolygon([System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF 98, 62), (New-Object System.Drawing.PointF 158, 62),
        (New-Object System.Drawing.PointF 146, 140), (New-Object System.Drawing.PointF 110, 140)))
    $g.FillPath($red, $body); $g.DrawPath($outline, $body)
    # Head: rounded cap on top.
    $head = New-Object System.Drawing.Drawing2D.GraphicsPath
    $head.AddEllipse(74, 14, 108, 62)
    $g.FillPath($red, $head); $g.DrawPath($outline, $head)
    # Highlight on the head (skipped at tiny sizes where it turns to mush).
    if ($size -ge 32) {
        $hl = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(150, 255, 255, 255))
        $g.FillEllipse($hl, 94, 24, 40, 18)
    }
    $g.Dispose()
    return $bmp
}

function Get-BmpFrame($bmp) {
    # ICO BMP frame: BITMAPINFOHEADER (height doubled), BGRA bottom-up, 1-bit AND mask.
    $w = $bmp.Width; $h = $bmp.Height
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([int]40); $bw.Write([int]$w); $bw.Write([int]($h * 2)); $bw.Write([int16]1); $bw.Write([int16]32)
    $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0); $bw.Write([int]0)
    for ($y = $h - 1; $y -ge 0; $y--) { for ($x = 0; $x -lt $w; $x++) {
        $c = $bmp.GetPixel($x, $y); $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A) } }
    $stride = [int]([math]::Ceiling($w / 32.0) * 4)
    for ($y = $h - 1; $y -ge 0; $y--) {
        $row = New-Object byte[] $stride
        for ($x = 0; $x -lt $w; $x++) { if ($bmp.GetPixel($x, $y).A -eq 0) { $row[[int][math]::Floor($x / 8)] = $row[[int][math]::Floor($x / 8)] -bor (0x80 -shr ($x % 8)) } }
        $bw.Write($row)
    }
    $bw.Flush(); return ,$ms.ToArray()
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$frames = @()
foreach ($sz in $sizes) {
    $b = New-Pin $sz
    if ($sz -eq 256) { $ms = New-Object System.IO.MemoryStream; $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $data = $ms.ToArray() }
    else { $data = Get-BmpFrame $b }
    $frames += ,@($sz, $data)
    $b.Dispose()
}
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $sz = $f[0]; $len = $f[1].Length
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $w.Write([byte]$dim); $w.Write([byte]$dim); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([int16]1); $w.Write([int16]32); $w.Write([int]$len); $w.Write([int]$offset)
    $offset += $len
}
foreach ($f in $frames) { $w.Write([byte[]]$f[1]) }
$w.Flush()
[System.IO.File]::WriteAllBytes($OutIco, $out.ToArray())

# Preview sheet: each size at 1:1 and 4x, on dark and light backgrounds,
# loaded back FROM the .ico for 16-48 px (checks those frames decode). The 256 px
# preview is rendered directly: System.Drawing.Icon(path, 256, 256) hands back the
# 64 px frame, so it cannot show the PNG frame.
$sheet = New-Object System.Drawing.Bitmap 1100, 560
$g = [System.Drawing.Graphics]::FromImage($sheet)
$g.InterpolationMode = 'NearestNeighbor'
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 32, 32, 42))), 0, 0, 1100, 280)
$g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 238, 238, 238))), 0, 280, 1100, 280)
$x = 10
foreach ($sz in 16, 24, 32, 48) {
    $ico = New-Object System.Drawing.Icon $OutIco, $sz, $sz
    $ib = $ico.ToBitmap()
    foreach ($band in 0, 280) {
        $g.DrawImage($ib, $x, $band + 10, $sz, $sz)
        $g.DrawImage($ib, $x, $band + 70, $sz * 4, $sz * 4)
    }
    $x += [math]::Max($sz * 4, 70) + 20
}
$big = New-Pin 256
$g.InterpolationMode = 'HighQualityBicubic'
$g.DrawImage($big, $x, 10, 256, 256); $g.DrawImage($big, $x, 290, 256, 256)
$g.Dispose()
$sheet.Save($PreviewPng)
"wrote $OutIco ($((Get-Item $OutIco).Length) bytes, $($frames.Count) frames)"
