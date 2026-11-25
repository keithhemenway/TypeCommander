@echo off
rem Builds TypeCommander with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No Visual Studio or SDK needed.
rem   Output: build\TypeCommander.exe and dist\TypeCommander-v<VERSION>-win.zip
setlocal
cd /d "%~dp0"

set /p VER=<VERSION
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe. .NET Framework 4.x is required.
  exit /b 1
)

if not exist obj mkdir obj
if not exist build mkdir build
if not exist dist mkdir dist

> obj\Version.cs (
  echo [assembly: System.Reflection.AssemblyVersion("%VER%.0"^)]
  echo [assembly: System.Reflection.AssemblyFileVersion("%VER%.0"^)]
  echo [assembly: System.Reflection.AssemblyInformationalVersion("%VER%"^)]
)

"%CSC%" /nologo /target:winexe /optimize+ /out:build\TypeCommander.exe /win32icon:src\app.ico ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll ^
  src\TypeCommander.cs src\AssemblyInfo.cs obj\Version.cs
if errorlevel 1 exit /b 1

copy /y README.md build\ >nul
copy /y CHANGELOG.md build\ >nul
set ZIP=dist\TypeCommander-v%VER%-win.zip
if exist "%ZIP%" del "%ZIP%"
powershell -NoProfile -Command "Compress-Archive -Path build\TypeCommander.exe,build\README.md,build\CHANGELOG.md -DestinationPath '%ZIP%'"
if errorlevel 1 exit /b 1

echo Built build\TypeCommander.exe (v%VER%)
echo Packaged %ZIP%
