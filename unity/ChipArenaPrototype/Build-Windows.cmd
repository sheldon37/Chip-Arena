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
if exist "%~dp0Builds\build.log" del /q "%~dp0Builds\build.log"
if exist "%~dp0Builds\build.log" (
  echo Cannot clear the previous build log. Close the running build or log viewer and try again.
  pause
  exit /b 1
)
echo Close this project in the Unity Editor before running this build.
echo Unity must be activated in Hub and Windows Build Support must be installed.
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
echo Play: "%~dp0Builds\Windows\ChipArena-Unity-TEST.exe"
echo The EXE carries the voxel CA icon. Keep its DLLs and Data folder together.
start "" "%~dp0Builds\Windows"
pause
