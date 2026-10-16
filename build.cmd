@echo off
rem Builds dsh-pet.exe next to this script (in-box csc, no SDK required).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
if errorlevel 1 (
  echo Build failed.
  exit /b 1
)
echo Build finished: dsh-pet.exe
