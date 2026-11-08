@echo off
setlocal
set "PROJECT=%~dp0..\.."
set "UNITY=D:\Software\Unity\6000.0.85f1\Editor\Unity.exe"
if not exist "%PROJECT%\Logs" mkdir "%PROJECT%\Logs"
echo [1/4] RulesCheck
dotnet build "%PROJECT%\Tools\RulesCheck\RulesCheck.csproj" --no-restore
if errorlevel 1 exit /b 10
echo [2/4] Unity script compilation
"%UNITY%" -batchmode -nographics -projectPath "%PROJECT%" -quit -logFile "%PROJECT%\Logs\unity-compile.log"
if errorlevel 1 exit /b 20
echo [3/4] EditMode tests
"%UNITY%" -batchmode -projectPath "%PROJECT%" -runTests -testPlatform EditMode -testResults "%PROJECT%\Logs\editmode-results.xml" -logFile "%PROJECT%\Logs\editmode.log"
if errorlevel 1 exit /b 30
echo [4/4] PlayMode tests
"%UNITY%" -batchmode -projectPath "%PROJECT%" -runTests -testPlatform PlayMode -testResults "%PROJECT%\Logs\playmode-results.xml" -logFile "%PROJECT%\Logs\playmode.log"
if errorlevel 1 exit /b 40
echo All automated checks passed.
endlocal
