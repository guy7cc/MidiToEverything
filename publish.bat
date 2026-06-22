@echo off
rem Build a single self-contained MidiToEverything.exe into .\publish (no .NET runtime needed
rem on the target machine). ASCII-only (cmd parses .bat with the OEM codepage).
chcp 65001 >nul
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo   [ERROR] .NET SDK not found. Install the .NET 8 SDK from https://dotnet.microsoft.com/download
  pause
  exit /b 1
)

echo   Publishing self-contained single-file build (win-x64)...
dotnet publish "src\App\App.csproj" -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -o "publish"
set EXITCODE=%ERRORLEVEL%

echo.
if "%EXITCODE%"=="0" (
  echo   Done. Output: %~dp0publish\MidiToEverything.exe
) else (
  echo   [WARN] Publish failed (code=%EXITCODE%). See messages above.
)
pause
endlocal
