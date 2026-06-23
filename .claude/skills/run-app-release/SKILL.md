---
name: run-app-release
description: Build and launch the latest Release build of the MidiToEverything WPF desktop app. Use when asked to run/start/launch the Release (optimized/production) build or confirm the Release build launches. Default is launch-only — no screenshots.
---

# Running MidiToEverything — Release build (WPF desktop app)

Native **Windows WPF** app (`net8.0-windows`). Same app as the Debug
[run-app](../run-app/SKILL.md) skill, but launches the **Release** (optimized) build. The goal
here is simply: **build the latest Release build and launch it.** Do not screenshot or drive the
UI unless the user explicitly asks to see or interact with a specific screen.

Use this skill (not `run-app`) when the user specifically asks for the Release / optimized /
production build. For day-to-day "just run it", `run-app` (Debug) is faster.

## 1. Build the latest Release build

```bash
dotnet build E:/GitHub/MidiToEverything/MidiToEverything.sln -c Release -nologo -clp:ErrorsOnly
```

Exe: `src/App/bin/Release/net8.0-windows/MidiToEverything.exe`.

(There are 2 warnings in Release; both predate this work and are harmless.)

## 2. Launch and confirm it started

```powershell
Start-Process "E:\GitHub\MidiToEverything\src\App\bin\Release\net8.0-windows\MidiToEverything.exe"
Start-Sleep -Seconds 3
Get-Process MidiToEverything -ErrorAction SilentlyContinue |
  Select-Object Id, MainWindowHandle, StartTime, Path
```

A process whose `Path` is under `...\Release\...` with a non-zero `MainWindowHandle` means the
window is up → done. That's the whole job; report success and stop.

## Notes

- **Two Release exes exist.** The solution build (`dotnet build -c Release`) produces
  `bin/Release/net8.0-windows/MidiToEverything.exe` — that's the one to launch. A second,
  RID-specific copy at `bin/Release/net8.0-windows/win-x64/MidiToEverything.exe` is produced by
  the distribution/publish (MSI) build; ignore it for a plain run.
- **Filter by `Path`** when both Debug and Release instances might be running — `Get-Process`
  alone can't tell them apart; check `.Path` contains `\Release\`.
- Custom chrome (`WindowStyle=None`), so `MainWindowTitle` may read empty — rely on
  `MainWindowHandle` being non-zero, not the title.
- The Release exe is locked while running — stop it (`Stop-Process`) before rebuilding Release.
  Don't kill the user's other instances without asking.
- The app needs no MIDI hardware to launch.
- The profile editor is a separate window (`プロファイル編集`) opened from the main window.
- **Only if** the user later asks to *see* a screen: computer-use `request_access` can't grant
  this unsigned dev build, so capture the window with PowerShell
  `System.Drawing.CopyFromScreen` (+ Win32 `GetWindowRect`) and Read the PNG. Not part of the
  default flow.
