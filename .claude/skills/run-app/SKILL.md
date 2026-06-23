---
name: run-app
description: Build and launch the latest Debug build of the MidiToEverything WPF desktop app. Use when asked to run/start the app or confirm it launches. Default is launch-only — no screenshots.
---

# Running MidiToEverything (WPF desktop app)

Native **Windows WPF** app (`net8.0-windows`). The goal here is simply: **build the latest
Debug build and launch it.** Do not screenshot or drive the UI unless the user explicitly
asks to see or interact with a specific screen.

## 1. Build the latest Debug build

```bash
dotnet build E:/GitHub/MidiToEverything/MidiToEverything.sln -nologo -clp:ErrorsOnly
```

Exe: `src/App/bin/Debug/net8.0-windows/MidiToEverything.exe`.

## 2. Launch and confirm it started

```powershell
Start-Process "E:\GitHub\MidiToEverything\src\App\bin\Debug\net8.0-windows\MidiToEverything.exe"
Start-Sleep -Seconds 3
Get-Process MidiToEverything -ErrorAction SilentlyContinue |
  Select-Object Id, MainWindowHandle, StartTime
```

A process with a non-zero `MainWindowHandle` means the window is up → done. That's the whole
job; report success and stop.

## Notes

- Custom chrome (`WindowStyle=None`), so `MainWindowTitle` may read empty — rely on
  `MainWindowHandle` being non-zero, not the title.
- An older instance may already be running (extra PID is harmless; don't kill the user's
  processes without asking).
- The app needs no MIDI hardware to launch.
- The profile editor is a separate window (`プロファイル編集`) opened from the main window.
- **Only if** the user later asks to *see* a screen: computer-use `request_access` can't grant
  this unsigned dev build (not Start-menu registered), so capture the window with PowerShell
  `System.Drawing.CopyFromScreen` (+ Win32 `GetWindowRect`) and Read the PNG. Not part of the
  default flow.
