@echo off
rem Double-clickable Windows launcher; see run-game.ps1.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run-game.ps1" %*
