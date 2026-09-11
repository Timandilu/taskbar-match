@echo off
setlocal
set "INSTALL=%LOCALAPPDATA%\TaskbarMatch"
set "PACKAGE=%~dp0"
set "HELPER=xaml-1.1.2-b1-1ca10b294157"

if exist "%INSTALL%\TaskbarMatch.exe" (
  start "" "%INSTALL%\TaskbarMatch.exe" %*
  exit /b 0
)

if not exist "%PACKAGE%TaskbarMatch.exe" (
  echo TaskbarMatch.exe is missing from this package.
  pause
  exit /b 1
)
if not exist "%PACKAGE%%HELPER%\TaskbarMatch.Xaml.dll" (
  echo TaskbarMatch.Xaml.dll is missing from this package.
  pause
  exit /b 1
)

if not exist "%INSTALL%" mkdir "%INSTALL%"
if not exist "%INSTALL%\%HELPER%" mkdir "%INSTALL%\%HELPER%"
copy /Y "%PACKAGE%TaskbarMatch.exe" "%INSTALL%TaskbarMatch.exe" >nul
copy /Y "%PACKAGE%%HELPER%\TaskbarMatch.Xaml.dll" "%INSTALL%\%HELPER%\TaskbarMatch.Xaml.dll" >nul
copy /Y "%PACKAGE%README.md" "%INSTALL%README.md" >nul
copy /Y "%PACKAGE%LICENSE.txt" "%INSTALL%LICENSE.txt" >nul
start "" "%INSTALL%\TaskbarMatch.exe" %*
