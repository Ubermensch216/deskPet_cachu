# ref/source_starters.png (이상해씨·피카츄·파이리·꼬부기 단체 그림) 에서 세 친구를 떼어 내고,
# 다른 친구에 가려지거나 잘린 부분을 도형으로 채워 ref/bulbasaur.png, charmander.png, squirtle.png 를 만든다.
param([string]$Src = "$PSScriptRoot\..\ref\source_starters.png", [string]$OutDir = "$PSScriptRoot\..\ref", [string]$Debug = "")
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition (Get-Content "$PSScriptRoot\StarterExtract.cs" -Raw -Encoding UTF8)
[StarterExtract]::Run((Resolve-Path $Src).Path, (Resolve-Path $OutDir).Path, $Debug)
