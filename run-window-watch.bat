@echo off
rem One-click active-window / profile-switch verification for M6. Double-click to run.
rem ASCII-only on purpose (cmd parses .bat with the OEM codepage). The Japanese UI
rem lives in the WindowWatch console app, which renders UTF-8 correctly.
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo.
echo   MidiToEverything - window / profile switch watch
echo   -------------------------------------------------
echo   Alt+Tab between Notepad / Browser / Explorer to see the profile auto-switch.
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

dotnet run --project "tools\WindowWatch\WindowWatch.csproj" -c Release --nologo
set EXITCODE=%ERRORLEVEL%

echo.
if not "%EXITCODE%"=="0" (
  echo   [WARN] Exited abnormally ^(code=%EXITCODE%^). See the messages above.
)
pause
endlocal
