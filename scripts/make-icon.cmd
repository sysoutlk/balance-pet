@echo off
rem 用法: make-icon.cmd -Source roles\你的图.png
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0make-icon.ps1" %*
