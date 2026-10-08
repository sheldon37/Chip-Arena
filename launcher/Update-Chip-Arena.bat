@echo off
cd /d "%~dp0"
"runtime\node.exe" "launcher\update.mjs" --force
pause
