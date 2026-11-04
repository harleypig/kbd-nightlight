# Removes the kbd-nightlight service and its files. Run as administrator.

#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

$name = 'kbd-nightlight'
$dest = Join-Path $env:ProgramFiles $name

$service = Get-Service $name -ErrorAction SilentlyContinue
if ($service) {
    if ($service.Status -ne 'Stopped') {
        Stop-Service $name
    }
    sc.exe delete $name | Out-Null
}

if ([System.Diagnostics.EventLog]::SourceExists($name)) {
    Remove-EventLog -Source $name
}

if (Test-Path $dest) {
    Remove-Item $dest -Recurse -Force
}

Write-Host "kbd-nightlight removed."
