@echo off
setlocal
if not defined UNITY_EDITOR set "UNITY_EDITOR=%ProgramFiles%\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe"
if not exist "%UNITY_EDITOR%" (
  echo Unity Editor was not found at "%UNITY_EDITOR%".
  echo Install Unity 6000.3.24f1 in Unity Hub, or set UNITY_EDITOR to your Unity.exe path.
  echo Open the project once in Unity Hub to finish setup and activate your own license.
  pause
  exit /b 1
)
if not exist "%~dp0Builds" mkdir "%~dp0Builds"
echo Close this project in the Unity Editor before running this build.
echo Building isolated Unity TEST package. Logs: "%~dp0Builds\build.log"
start "" /wait "%UNITY_EDITOR%" -batchmode -nographics -projectPath "%~dp0." -buildTarget Win64 -executeMethod ChipArena.UnityPrototype.Editor.PrototypeBuild.BuildWindows -quit -logFile "%~dp0Builds\build.log"
if errorlevel 1 (
  echo Build failed. See Builds\build.log. No verified EXE was produced by this invocation.
  pause
  exit /b 1
)
findstr /C:"UNITY_TEST_BUILD_READY:" "%~dp0Builds\build.log" >nul
if errorlevel 1 (
  echo Unity did not confirm a completed test build. See Builds\build.log.
  pause
  exit /b 1
)
echo Ready: "%~dp0Builds\Chip-Arena-Unity-TEST-Windows.zip"
pause
