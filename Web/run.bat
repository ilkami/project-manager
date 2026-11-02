@echo off
chcp 65001 >nul
cd /d "%~dp0"

set PY=py -3
%PY% -c "import sys" >nul 2>&1
if errorlevel 1 set PY=python

%PY% -c "import sys" >nul 2>&1
if errorlevel 1 (
    echo Python not found. Install Python 3.11+ from https://www.python.org/downloads/
    echo During setup enable "Add python.exe to PATH".
    pause
    exit /b 1
)

echo Installing dependencies...
%PY% -m pip install --quiet --disable-pip-version-check -r requirements.txt
if errorlevel 1 (
    echo Failed to install dependencies.
    pause
    exit /b 1
)

echo.
echo Server: http://127.0.0.1:5000
echo Login: admin / admin123  (administrator),  ivanov / user123  (user)
echo Stop: Ctrl+C
echo.

%PY% app.py
pause
