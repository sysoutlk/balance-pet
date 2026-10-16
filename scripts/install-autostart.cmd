@echo off
rem 加入开机自启（取消请用 -Uninstall 参数）
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install-autostart.ps1" %*
