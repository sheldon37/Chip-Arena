@echo off
setlocal
cd /d "%~dp0"
title Chip Arena Online Server
"%~dp0runtime\node.exe" "%~dp0launcher\start.mjs" --online
if errorlevel 1 pause
