@echo off
rem One-click real key-injection verification for M5. Double-click to run.
rem ASCII-only on purpose (cmd parses .bat with the OEM codepage). The Japanese UI
rem lives in the KeyTest console app, which renders UTF-8 correctly.
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo.
echo   MidiToEverything - key injection test
echo   --------------------------------------
echo   Playing the MIDI device types letters into the FOCUSED window.
echo   Open Notepad and bring it to the front before playing.
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

dotnet run --project "tools\KeyTest\KeyTest.csproj" -c Release --nologo
set EXITCODE=%ERRORLEVEL%

echo.
if not "%EXITCODE%"=="0" (
  echo   [WARN] Exited abnormally ^(code=%EXITCODE%^). See the messages above.
)
pause
endlocal
