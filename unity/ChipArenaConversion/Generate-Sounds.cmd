@echo off
setlocal
py -3 "%~dp0tools\generate_sound_requests.py" %*
set "sound_result=%errorlevel%"
if not "%sound_result%"=="0" echo Sound generation stopped. Completed files are preserved. See docs/audio/SOUND-GENERATOR.md.
pause
exit /b %sound_result%
