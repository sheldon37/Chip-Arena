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
echo This replaces Builds\Windows and the starter ZIP. Keep your installed test copy outside this project.
echo Building isolated Unity TEST package. Logs: "%~dp0Builds\build.log"
start "" /wait "%UNITY_EDITOR%" -batchmode -nographics -projectPath "%~dp0." -buildTarget Win64 -executeMethod ChipArena.UnityConversion.Editor.PrototypeBuild.BuildWindows -quit -logFile "%~dp0Builds\build.log"
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
echo Ready: "%~dp0Builds\Chip-Arena-Windows.zip"
echo Preview newest build: "%~dp0Builds\Windows\ChipArena-Native-Conversion-TEST.exe"
echo The EXE carries the voxel CA icon. Keep the launcher and app folder together.
echo First install: extract the ZIP outside this project, then open Chip Arena\Chip Arena.exe.
echo Test future updates from that older installed copy, not from Builds\Windows.
echo Update feed prepared locally at Builds\NativeUpdateFeed; nothing was published.
echo Compare published status: Publish-Native.cmd --status
echo After testing this build, run Publish-Native.cmd to review and publish it.
start "" "%~dp0Builds\Windows"
pause
