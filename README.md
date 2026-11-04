# kbd-nightlight
Windows service that turns on the ThinkPad keyboard backlight at the lock screen

Tested on a ThinkPad P17 Gen 1. It talks to Lenovo's PM driver (`\\.\IBMPmDrv`), so it needs the Lenovo power-management driver that ships with ThinkPads.

## Build

Uses the C# compiler that comes with Windows (.NET Framework 4.8). No SDK needed.

```powershell
.\build.ps1
```

## kbdlight

Command-line tool to read or set the backlight. No administrator rights needed.

```powershell
.\bin\kbdlight.exe get              # off, low or high
.\bin\kbdlight.exe set high         # off | low | high
.\bin\kbdlight.exe raw              # raw driver value, for debugging
```
