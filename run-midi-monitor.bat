@echo off
rem One-click MIDI input verification for M4. Double-click to run.
rem NOTE: keep this file ASCII-only. cmd.exe parses .bat using the OEM codepage
rem (CP932 on Japanese Windows), so non-ASCII text here breaks parsing. The rich
rem Japanese UI lives in the MidiMonitor console app, which renders UTF-8 correctly.
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo.
echo   MidiToEverything - MIDI input monitor
echo   --------------------------------------
echo   Building and launching via the .NET SDK. The first run may take a while.
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
  echo   [ERROR] .NET SDK not found.
  echo   Install the .NET 8 SDK from https://dotnet.microsoft.com/download
  echo.
  pause
  exit /b 1
)

dotnet run --project "tools\MidiMonitor\MidiMonitor.csproj" -c Release --nologo
set EXITCODE=%ERRORLEVEL%

echo.
if not "%EXITCODE%"=="0" (
  echo   [WARN] Exited abnormally ^(code=%EXITCODE%^). See the messages above.
)
pause
endlocal
