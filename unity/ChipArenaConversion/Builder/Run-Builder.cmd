@echo off
setlocal
py -3 -c "import sys; assert sys.version_info >= (3,8)" >nul 2>nul
if not errorlevel 1 (
  py -3 "%~dp0builder.py" %*
  exit /b
)
python -c "import sys; assert sys.version_info >= (3,8)" >nul 2>nul
if not errorlevel 1 (
  python "%~dp0builder.py" %*
  exit /b
)
echo Install Python 3.8 or newer, then reopen this builder.
exit /b 1
