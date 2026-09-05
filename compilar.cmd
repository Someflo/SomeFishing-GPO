@echo off
setlocal
cd /d "%~dp0"
set "PESCA_CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%PESCA_CSC%" (
  echo No se encontro el compilador de .NET Framework de Windows.
  exit /b 1
)
set "PESCA_WINMD="
for /f "delims=" %%D in ('dir /b /ad /o-n "%ProgramFiles(x86)%\Windows Kits\10\UnionMetadata" 2^>nul') do if not defined PESCA_WINMD if exist "%ProgramFiles(x86)%\Windows Kits\10\UnionMetadata\%%D\Facade\Windows.winmd" if exist "%ProgramFiles(x86)%\Windows Kits\10\UnionMetadata\%%D\Windows.winmd" set "PESCA_WINMD=%ProgramFiles(x86)%\Windows Kits\10\UnionMetadata\%%D\Windows.winmd"
if not defined PESCA_WINMD (
  echo Para compilar el lector de cebo necesitas el Windows 10 o Windows 11 SDK.
  exit /b 1
)
set "PESCA_RUNTIME="
for /d %%D in ("%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\System.Runtime\v4.0_*") do if exist "%%D\System.Runtime.dll" set "PESCA_RUNTIME=%%D\System.Runtime.dll"
if not defined PESCA_RUNTIME (
  echo No se encontro System.Runtime. Requiere .NET Framework 4.8.
  exit /b 1
)
"%PESCA_CSC%" /nologo /target:winexe /platform:x64 /optimize+ /warn:4 /out:SomeFishingGPO.exe /win32manifest:src\app.manifest /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:"%PESCA_WINMD%" /reference:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Runtime.WindowsRuntime.dll" /reference:"%PESCA_RUNTIME%" src\Core.cs src\Native.cs src\App.cs src\Tests.cs src\Bait.cs src\WindowsBaitReader.cs src\CounterGlyphs.cs src\Shop.cs src\ShopLabels.cs src\WindowsShopReader.cs src\AssemblyInfo.cs
if errorlevel 1 exit /b 1
echo Compilado: SomeFishingGPO.exe
endlocal
