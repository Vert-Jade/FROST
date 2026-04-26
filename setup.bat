@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup.ps1" -Configuration Release -Run
set "exit_code=%ERRORLEVEL%"
if not "%exit_code%"=="0" (
    echo.
    echo FROST setup failed with exit code %exit_code%.
    pause
    exit /b %exit_code%
)
endlocal
