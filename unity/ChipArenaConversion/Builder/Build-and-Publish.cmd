@echo off
setlocal
echo Chip Arena - Fetch, Build and Publish
echo A successful validated build will be uploaded to the test Update channel.
call "%~dp0Run-Builder.cmd" build-publish %*
set "BUILDER_RESULT=%ERRORLEVEL%"
pause
exit /b %BUILDER_RESULT%
