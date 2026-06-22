@echo off
rem One-click MIDI input verification for M4. Double-click to run.
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo.
echo   MidiToEverything - MIDI input monitor
echo   --------------------------------------
echo   .NET SDK でビルドして起動します。初回は少し時間がかかります。
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
  echo   [エラー] .NET SDK が見つかりません。
  echo   https://dotnet.microsoft.com/download から .NET 8 SDK をインストールしてください。
  echo.
  pause
  exit /b 1
)

dotnet run --project "tools\MidiMonitor\MidiMonitor.csproj" -c Release --nologo
set EXITCODE=%ERRORLEVEL%

echo.
if not "%EXITCODE%"=="0" (
  echo   [警告] 異常終了しました (code=%EXITCODE%)。上のメッセージを確認してください。
)
pause
endlocal
