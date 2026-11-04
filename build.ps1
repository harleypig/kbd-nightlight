# Builds bin\kbdlight.exe and bin\kbd-nightlight.exe with the C# compiler
# that ships with Windows (.NET Framework 4.8). No SDK needed.

$ErrorActionPreference = 'Stop'

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "C# compiler not found at $csc" }

$src = Join-Path $PSScriptRoot 'src'
$bin = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force $bin | Out-Null

function Build($out, [string[]]$files, [string[]]$refs = @()) {
    $sources = $files | ForEach-Object { Join-Path $src $_ }
    $references = $refs | ForEach-Object { "/reference:$_" }
    & $csc /nologo /optimize+ /platform:anycpu /target:exe "/out:$(Join-Path $bin $out)" $references $sources
    if ($LASTEXITCODE -ne 0) { throw "csc failed building $out (exit code $LASTEXITCODE)" }
    Write-Host "Built $(Join-Path $bin $out)"
}

Build 'kbdlight.exe' 'Backlight.cs', 'KbdLight.cs'
Build 'kbd-nightlight.exe' 'Backlight.cs', 'Session.cs', 'Nightlight.cs', 'Service.cs' 'System.Configuration.dll'
Copy-Item (Join-Path $src 'kbd-nightlight.exe.config') $bin -Force
