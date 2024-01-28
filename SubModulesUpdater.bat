@echo off
set dir="%~dp0refs\SilDev\CSharpLib"
if not exist %dir% exit /B 1
cd /D %dir%
git reset --hard
git pull origin master
pause
exit /B 0
