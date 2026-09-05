@echo off
setlocal
cd /d "%~dp0"
set "PESCA_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%PESCA_CSC%" (
  echo No se encontro el compilador de .NET Framework de Windows.
  exit /b 1
)
"%PESCA_CSC%" /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /out:SomeFishingGPO.exe /win32manifest:src\app.manifest /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll src\Core.cs src\Native.cs src\App.cs src\Tests.cs src\AssemblyInfo.cs
if errorlevel 1 exit /b 1
echo Compilado: SomeFishingGPO.exe
endlocal
