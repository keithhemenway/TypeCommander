@echo off
rem Builds TypeCommander.exe with the C# compiler that ships with Windows (.NET Framework 4.x).
rem No Visual Studio or SDK needed.
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe. .NET Framework 4.x is required.
  exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /out:TypeCommander.exe /win32icon:src\app.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll src\TypeCommander.cs
if errorlevel 1 exit /b 1
echo Built TypeCommander.exe
