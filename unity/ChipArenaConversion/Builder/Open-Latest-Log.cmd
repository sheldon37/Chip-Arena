@echo off
if not exist "%~dp0Latest-Build.log" (
  echo No build log yet. Run Build-and-Publish.cmd first.
  pause
  exit /b 1
)
start "" notepad.exe "%~dp0Latest-Build.log"
