@echo off
setlocal
rem Status is read-only. Publication always asks for this build's confirmation.
if not "%~1"=="" if /I not "%~1"=="--status" goto usage
if not "%~2"=="" goto usage
py -3 -c "import sys; assert sys.version_info >= (3,8)" >nul 2>nul
if not errorlevel 1 (
  set "NATIVE_PYTHON=py -3"
  goto run
)
python -c "import sys; assert sys.version_info >= (3,8)" >nul 2>nul
if not errorlevel 1 (
  set "NATIVE_PYTHON=python"
  goto run
)
echo Python 3.8 or newer is required. Install Python and run this command again.
pause
exit /b 1
:run
if /I "%~1"=="--status" (
  %NATIVE_PYTHON% "%~dp0tools\publish_native_update.py" --feed "%~dp0Builds\NativeUpdateFeed" --status
) else (
  %NATIVE_PYTHON% "%~dp0tools\publish_native_update.py" --feed "%~dp0Builds\NativeUpdateFeed" --build-log "%~dp0Builds\build.log" --channel-checkout "%~dp0Builds\native-channel-checkout" --publish --confirm
)
set "NATIVE_RESULT=%ERRORLEVEL%"
if not "%NATIVE_RESULT%"=="0" echo Publication did not complete. Read the message above before retrying.
pause
exit /b %NATIVE_RESULT%
:usage
echo Usage: Publish-Native.cmd [--status]
echo With --status: validate the local player package and compare the public feed, without writing.
echo Without arguments: validate, preview and ask to publish the completed Windows build.
pause
exit /b 1
