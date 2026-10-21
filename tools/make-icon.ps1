# ref 이미지(pose4)로 PNG 압축 방식의 .ico 를 만든다.
param([string]$Out, [string]$SrcPath = "$PSScriptRoot\..\ref\pose4.png")
Add-Type -AssemblyName System.Drawing
$src = [Drawing.Image]::FromFile((Resolve-Path $SrcPath))
$sizes = 16, 32, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $bmp = New-Object Drawing.Bitmap $s, $s, ([Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.Clear([Drawing.Color]::Transparent)
    $g.InterpolationMode = 'HighQualityBicubic'; $g.SmoothingMode = 'HighQuality'; $g.PixelOffsetMode = 'HighQuality'
    $f = [Math]::Min(($s - 2) / $src.Width, ($s - 2) / $src.Height)
    $w = $src.Width * $f; $h = $src.Height * $f
    $g.DrawImage($src, ($s - $w) / 2, ($s - $h) / 2, $w, $h)
    $g.Dispose()
    $ms = New-Object IO.MemoryStream
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
    ,$ms.ToArray()
}
$src.Dispose()
New-Item -ItemType Directory -Force (Split-Path -Parent $Out) | Out-Null
$fs = [IO.File]::Create($Out); $bw = New-Object IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $b = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$b); $bw.Write([byte]$b); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Close(); $fs.Close()
Write-Host "아이콘 생성: $Out"
