@echo off
setlocal
cd /d "%~dp0"
title Chip Arena Server
"%~dp0runtime\node.exe" "%~dp0launcher\start.mjs"
if errorlevel 1 pause
