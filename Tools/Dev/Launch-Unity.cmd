@echo off
setlocal
set "PROJECT=%~dp0..\.."
set "UNITY=D:\Software\Unity\6000.0.85f1\Editor\Unity.exe"
if not exist "%UNITY%" (
  echo Unity executable not found: %UNITY%
  exit /b 2
)
if not exist "%PROJECT%\Logs" mkdir "%PROJECT%\Logs"
start "Mahjong Unity Editor" "%UNITY%" -projectPath "%PROJECT%" -logFile "%PROJECT%\Logs\unity-editor.log" -force-d3d11
endlocal
