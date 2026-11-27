@echo off
cd /d "%~dp0..\.."
powershell.exe -NoLogo -NoProfile -File "%~dp0..\..\Install.ps1"
if errorlevel 1 (
  echo Installation did not finish. Read README.txt or run app\DeskDisplay.App.exe directly.
)
pause
