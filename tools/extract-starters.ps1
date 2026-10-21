# ref/source_starters.png (이상해씨·피카츄·파이리·꼬부기 단체 그림) 에서 세 친구를 떼어 내고,
# 다른 친구에 가려지거나 잘린 부분을 도형으로 채워 ref/bulbasaur.png, charmander.png, squirtle.png (+ squirtle_shell.png) 를 만든다.
param([string]$Src = "$PSScriptRoot\..\ref\source_starters.png", [string]$OutDir = "$PSScriptRoot\..\ref", [string]$Debug = "")
$main = Get-Content "$PSScriptRoot\StarterExtract.cs" -Raw -Encoding UTF8
$recon = (Get-Content "$PSScriptRoot\Reconstruct.cs" -Raw -Encoding UTF8) -replace '(?m)^using [^\r\n]*[\r\n]+', ''
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition ($main + "`n" + $recon)
[StarterExtract]::Run((Resolve-Path $Src).Path, (Resolve-Path $OutDir).Path, $Debug)
