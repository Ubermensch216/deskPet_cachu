# 확인용: ref\bulbasaur/charmander/squirtle.png 를 한 장에 나란히 확대해 obj\final_all.png 로 저장한다.
param([string]$Out = "$PSScriptRoot\..\obj\final_all.png")
Add-Type -AssemblyName System.Drawing
$ref = "$PSScriptRoot\..\ref"
$o = New-Object System.Drawing.Bitmap 1500, 620
$g = [System.Drawing.Graphics]::FromImage($o); $g.Clear([System.Drawing.Color]::FromArgb(90, 110, 170))
$x = 5
$items = @(@('bulbasaur', 100, 90, 330, 350), @('charmander', 420, 110, 330, 340), @('squirtle', 560, 110, 330, 340))
foreach ($it in $items) {
    $b = [System.Drawing.Bitmap]::FromFile((Join-Path $ref ($it[0] + '.png')))
    $rect = New-Object System.Drawing.Rectangle $it[1], $it[2], $it[3], $it[4]
    $dest = New-Object System.Drawing.Rectangle $x, 5, ([int]($it[3] * 1.45)), ([int]($it[4] * 1.45))
    $g.DrawImage($b, $dest, $rect, [System.Drawing.GraphicsUnit]::Pixel)
    $x += $dest.Width + 5; $b.Dispose()
}
$o.Save((Resolve-Path (Split-Path $Out)).Path + '\' + (Split-Path $Out -Leaf))
