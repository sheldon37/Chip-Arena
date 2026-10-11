@echo off
setlocal
call "%~dp0Run-Builder.cmd" status
set "BUILDER_RESULT=%ERRORLEVEL%"
pause
exit /b %BUILDER_RESULT%
