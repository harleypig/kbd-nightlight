# Installs (or updates) the kbd-nightlight service. Run as administrator.
# Builds first, copies the files to Program Files, and starts the service.
# An existing kbd-nightlight.exe.config in the install folder is kept.

#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

$name = 'kbd-nightlight'
$dest = Join-Path $env:ProgramFiles $name
$exe = Join-Path $dest 'kbd-nightlight.exe'
$bin = Join-Path $PSScriptRoot 'bin'

& (Join-Path $PSScriptRoot 'build.ps1')

$existing = Get-Service $name -ErrorAction SilentlyContinue
if ($existing -and $existing.Status -ne 'Stopped') {
    Stop-Service $name
}

New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $bin 'kbd-nightlight.exe') $dest -Force
Copy-Item (Join-Path $bin 'kbdlight.exe') $dest -Force
$config = Join-Path $dest 'kbd-nightlight.exe.config'
if (-not (Test-Path $config)) {
    Copy-Item (Join-Path $bin 'kbd-nightlight.exe.config') $dest
}

if (-not [System.Diagnostics.EventLog]::SourceExists($name)) {
    New-EventLog -LogName Application -Source $name
}

if (-not $existing) {
    New-Service -Name $name -BinaryPathName "`"$exe`"" -DisplayName 'Keyboard nightlight' `
        -Description 'Turns on the ThinkPad keyboard backlight at the lock and login screen.' `
        -StartupType Automatic | Out-Null
    # Restart after a crash: 5 s, 30 s, then every 60 s.
    sc.exe failure $name reset= 86400 actions= restart/5000/restart/30000/restart/60000 | Out-Null
}

Start-Service $name
Get-Service $name | Format-Table Name, Status, StartType -AutoSize
