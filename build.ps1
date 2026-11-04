# Builds bin\kbdlight.exe with the C# compiler that ships with Windows
# (.NET Framework 4.8). No SDK needed.

$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }

$src = Join-Path $PSScriptRoot 'src'
$bin = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

& $csc /nologo /optimize+ /platform:anycpu /target:exe `
    "/out:$(Join-Path $bin 'kbdlight.exe')" `
    (Join-Path $src 'Backlight.cs') (Join-Path $src 'KbdLight.cs')
if ($LASTEXITCODE -ne 0) { throw "csc failed with exit code $LASTEXITCODE" }

Write-Host "Built $(Join-Path $bin 'kbdlight.exe')"
