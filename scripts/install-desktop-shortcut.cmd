@echo off
rem 在桌面创建「DSH 桌宠」快捷方式
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-desktop-shortcut.ps1" %*
