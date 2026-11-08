@echo off
setlocal
set "PROJECT=%~dp0..\.."
set "UNITY=D:\Software\Unity\6000.0.85f1\Editor\Unity.exe"
set "SHOT_DIR=%PROJECT%\Screenshots\current\dev-capture"
if not exist "%PROJECT%\Logs" mkdir "%PROJECT%\Logs"
if not exist "%SHOT_DIR%" mkdir "%SHOT_DIR%"
set "MAHJONG_SHOT_DIR=%SHOT_DIR%"
set "MAHJONG_CAPTURE_STEPS=welcome,table-overview,menu-panel,result-screen"
set "MAHJONG_TITLE=0"
"%UNITY%" -batchmode -projectPath "%PROJECT%" -executeMethod Mahjong.EditorTools.InterfaceCapture.CaptureAndExit -logFile "%PROJECT%\Logs\ui-capture.log"
set "CODE=%ERRORLEVEL%"
echo Capture exit code: %CODE%
echo Screenshots: %SHOT_DIR%
endlocal & exit /b %CODE%
