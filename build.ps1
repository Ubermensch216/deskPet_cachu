# 카츄 빌드 스크립트
# .NET SDK 의 Roslyn(csc.dll)로 .NET Framework 4.8 대상 단일 EXE 를 만든다. (설치 없이 EXE 한 개로 실행)
param([string]$Out = "$PSScriptRoot\output\카츄.exe")
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dotnet = (Get-Command dotnet).Source

# 설치 위치가 PC마다 달라서(Program Files / 사용자 폴더) dotnet 이 알려 주는 SDK 경로를 우선 사용한다.
$sdkDirs = New-Object System.Collections.Generic.List[string]
& $dotnet --list-sdks | ForEach-Object { if ($_ -match '^\S+\s+\[(.+)\]$') { $sdkDirs.Add($Matches[1]) } }
$sdkDirs.Add('C:\Program Files\dotnet\sdk')
$csc = $sdkDirs | ForEach-Object { Get-ChildItem (Join-Path $_ '*\Roslyn\bincore\csc.dll') -ErrorAction SilentlyContinue } |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $csc) { throw 'Roslyn csc.dll 을 찾지 못했어요. .NET SDK 를 설치해 주세요.' }
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$refs = 'mscorlib','System','System.Core','System.Drawing','System.Windows.Forms','System.Xml','System.Xml.Linq'

New-Item -ItemType Directory -Force "$root\output","$root\obj" | Out-Null
$icon = "$root\assets\cachu.ico"
if (-not (Test-Path $icon)) { & "$PSScriptRoot\tools\make-icon.ps1" -Out $icon }

$rsp = New-Object System.Collections.Generic.List[string]
$rsp.Add('/noconfig'); $rsp.Add('/nostdlib+'); $rsp.Add('/target:winexe'); $rsp.Add('/optimize+'); $rsp.Add('/platform:anycpu')
$rsp.Add('/nowarn:CS0649,CS0169,CS0414,CS0219'); $rsp.Add('/utf8output'); $rsp.Add('/langversion:latest')
$rsp.Add('/out:"' + $Out + '"'); $rsp.Add('/win32icon:"' + $icon + '"'); $rsp.Add('/win32manifest:"' + "$root\assets\app.manifest" + '"')
foreach ($r in $refs) { $rsp.Add('/r:"' + "$fw\$r.dll" + '"') }
# ref\pose*.png (배경을 지운 포즈 그림; tools\extract-poses.ps1 이 source_sheet.png 에서 만든다)
foreach ($n in 1,2,3,4,6) { $rsp.Add('/resource:"' + "$root\ref\pose$n.png" + '",' + "pose$n.png") }
# 이상해씨·파이리·꼬부기 (tools\extract-starters.ps1 이 단체 그림에서 만든 그림)
foreach ($n in 'bulbasaur','charmander','squirtle','squirtle_shell') { $rsp.Add('/resource:"' + "$root\ref\$n.png" + '",' + "$n.png") }
Get-ChildItem "$root\src\*.cs" | ForEach-Object { $rsp.Add('"' + $_.FullName + '"') }
$rspPath = "$root\obj\build.rsp"
[IO.File]::WriteAllLines($rspPath, $rsp, (New-Object Text.UTF8Encoding($true)))
& $dotnet $csc.FullName "@$rspPath"
if ($LASTEXITCODE -ne 0) { throw "빌드 실패 (exit $LASTEXITCODE)" }
Write-Host ("빌드 완료: {0} ({1:N0} bytes)" -f $Out, (Get-Item $Out).Length)
