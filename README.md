# kbd-nightlight
Windows service that turns on the ThinkPad keyboard backlight at the lock screen

Tested on a ThinkPad P17 Gen 1. It talks to Lenovo's PM driver (`\\.\IBMPmDrv`), so it needs the Lenovo power-management driver that ships with ThinkPads.

## Build

Uses the C# compiler that comes with Windows (.NET Framework 4.8). No SDK needed.

```powershell
.\build.ps1
```

## Install the service

Run in an administrator PowerShell:

```powershell
.\install.ps1      # build, copy to C:\Program Files\kbd-nightlight, start the service
.\uninstall.ps1    # stop and remove everything
```

Running `install.ps1` again updates the service and keeps your config file.

## What the service does

- **At the lock or login screen** (with the lid open): sets the backlight to the lock level, default `high`. It does this when the service starts, when the laptop wakes from sleep or hibernate, and when the screen locks. While locked, it checks again every 30 seconds.
- **On unlock or logon:** sets the unlock level once, default `low`. While you're unlocked it never touches the backlight, so Fn+Space works as usual.

Settings are in `C:\Program Files\kbd-nightlight\kbd-nightlight.exe.config`: `LockLevel`, `UnlockLevel`, `IntervalSeconds`. Restart the service after changing them:

```powershell
Restart-Service kbd-nightlight
```

Every change it makes is written to the Application event log under the source `kbd-nightlight`:

```powershell
Get-WinEvent -FilterHashtable @{LogName='Application'; ProviderName='kbd-nightlight'} -MaxEvents 20
```

For testing, `kbd-nightlight.exe --status` shows what the service sees right now (session, lock state, backlight level).

## kbdlight

Command-line tool to read or set the backlight. No administrator rights needed.

```powershell
.\bin\kbdlight.exe get              # off, low or high
.\bin\kbdlight.exe set high         # off | low | high
.\bin\kbdlight.exe raw              # raw driver value, for debugging
```
