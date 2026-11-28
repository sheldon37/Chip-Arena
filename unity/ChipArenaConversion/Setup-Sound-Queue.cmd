@echo off
py -3 "%~dp0tools\setup_sound_queue.py"
if errorlevel 1 pause
