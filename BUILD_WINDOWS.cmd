@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\build-windows.ps1"
if errorlevel 1 (
  echo.
  echo HASHME build failed with exit code %errorlevel%.
) else (
  echo.
  echo HASHME v1.2 build completed successfully.
)
pause
