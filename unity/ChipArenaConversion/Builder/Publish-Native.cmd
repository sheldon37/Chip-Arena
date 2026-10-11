@echo off
setlocal
if /I "%~1"=="--status" (
  call "%~dp0Run-Builder.cmd" status
) else (
  if not "%~1"=="" (
    echo Usage: Publish-Native.cmd [--status]
    pause
    exit /b 1
  )
  call "%~dp0Run-Builder.cmd" publish
)
set "BUILDER_RESULT=%ERRORLEVEL%"
pause
exit /b %BUILDER_RESULT%
