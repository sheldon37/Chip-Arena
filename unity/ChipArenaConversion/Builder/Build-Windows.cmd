@echo off
setlocal
call "%~dp0Run-Builder.cmd" build %*
set "BUILDER_RESULT=%ERRORLEVEL%"
pause
exit /b %BUILDER_RESULT%
