@echo off
title LznTweaker - Gerador de Executavel Final
cd /d "%~dp0"
echo Iniciando compilacao do LznTweaker...
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1" %*
echo.
pause
