@echo off
REM Headless Unity tasks (Windows). Usage:
REM   scripts\unity-tasks.bat setup
REM   scripts\unity-tasks.bat build-quest
REM   scripts\unity-tasks.bat build-webgl
REM Set UNITY_PATH if Unity is not in the default Hub location.

setlocal
set PROJECT_ROOT=%~dp0..
for /f "tokens=2" %%v in ('findstr /b "m_EditorVersion:" "%PROJECT_ROOT%\ProjectSettings\ProjectVersion.txt"') do set VERSION=%%v

if "%UNITY_PATH%"=="" set UNITY_PATH=C:\Program Files\Unity\Hub\Editor\%VERSION%\Editor\Unity.exe
if not exist "%UNITY_PATH%" (
  echo ERROR: Unity %VERSION% not found at "%UNITY_PATH%". Set UNITY_PATH. 1>&2
  exit /b 1
)

if "%1"=="setup"       set METHOD=NSFGrant.EditorTools.CiTools.SetupProject
if "%1"=="build-quest" set METHOD=NSFGrant.EditorTools.CiTools.BuildQuest
if "%1"=="build-webgl" set METHOD=NSFGrant.EditorTools.CiTools.BuildWebGL
if "%METHOD%"=="" (
  echo Usage: %0 {setup^|build-quest^|build-webgl} 1>&2
  exit /b 64
)

echo ^>^> Unity: %UNITY_PATH%
echo ^>^> Method: %METHOD%  (first run imports packages and can take a while)
"%UNITY_PATH%" -batchmode -nographics -quit -projectPath "%PROJECT_ROOT%" -executeMethod %METHOD% -logFile -
