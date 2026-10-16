@echo off
rem 在桌面创建「余额桌宠」快捷方式
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-desktop-shortcut.ps1" %*
