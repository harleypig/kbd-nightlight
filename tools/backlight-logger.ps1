# Read-only logger for issue #3: every few seconds, records the keyboard
# backlight level along with the things that might turn it off. Never sets
# the backlight. Stops after -Hours, or at reboot.
#
#   Start-Process powershell.exe -WindowStyle Hidden -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File tools\backlight-logger.ps1'

param(
    [int]$IntervalSeconds = 10,
    [double]$Hours = 12,
    [string]$LogFile = (Join-Path $env:LOCALAPPDATA 'kbd-nightlight\backlight-log.csv')
)

$ErrorActionPreference = 'Stop'

Add-Type -Path (Join-Path $PSScriptRoot '..\src\Backlight.cs')
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class Idle {
    [StructLayout(LayoutKind.Sequential)] struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LASTINPUTINFO info);
    public static uint Seconds() {
        var info = new LASTINPUTINFO(); info.cbSize = (uint)Marshal.SizeOf(info);
        GetLastInputInfo(ref info);
        return ((uint)Environment.TickCount - info.dwTime) / 1000;
    }
}
'@

New-Item -ItemType Directory -Force (Split-Path $LogFile) | Out-Null
if (-not (Test-Path $LogFile)) {
    'time,level,raw,power,locked,screensaver,idle_seconds,changed' | Set-Content $LogFile
}
"# started $(Get-Date -Format s), pid $PID" | Add-Content $LogFile

$end = (Get-Date).AddHours($Hours)
$last = $null
while ((Get-Date) -lt $end) {
    try {
        $raw = [KbdNightlight.Backlight]::Read()
        $level = ([KbdNightlight.BacklightLevel]($raw -band 0xF)).ToString().ToLowerInvariant()
        $rawText = '0x{0:X8}' -f $raw
    } catch {
        $level = 'error'; $rawText = $_.Exception.Message -replace ',', ';'
    }
    $power = [System.Windows.Forms.SystemInformation]::PowerStatus.PowerLineStatus
    $locked = [bool](Get-Process LogonUI -ErrorAction SilentlyContinue)
    $saver = [bool](Get-Process infinidream* -ErrorAction SilentlyContinue)
    $changed = if ($last -ne $null -and $level -ne $last) { 'CHANGED' } else { '' }

    '{0},{1},{2},{3},{4},{5},{6},{7}' -f (Get-Date -Format s), $level, $rawText, $power, $locked, $saver, [Idle]::Seconds(), $changed |
        Add-Content $LogFile

    $last = $level
    Start-Sleep -Seconds $IntervalSeconds
}
"# stopped $(Get-Date -Format s)" | Add-Content $LogFile
