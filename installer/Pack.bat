@echo off
setlocal EnableExtensions
cd /d "%~dp0.."
set "ROOT=%CD%"
set "INST=%ROOT%\installer"
set "DIST=%ROOT%\dist"
set "PAYLOAD=%INST%\payload"
set "ZIP=%INST%\payload.zip"
set "NATIVE=%ROOT%\native\x64"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

echo USBCAN Tool packer (cmd, no PowerShell)

if not exist "%CSC%" (
  echo ERROR: csc.exe not found at %CSC%
  echo Install .NET Framework 4.8 Developer Pack, or build USBCANToolSetup in Visual Studio.
  exit /b 1
)

set "SDK="
if exist "%NATIVE%\zlgcan.dll" set "SDK=%NATIVE%"
if not defined SDK if defined ZLG_CAN_SDK if exist "%ZLG_CAN_SDK%\zlgcan.dll" set "SDK=%ZLG_CAN_SDK%"
if not defined SDK if exist "C:\Users\allahmoradi\Downloads\zlgcan(261014)\zlgcan_x64\zlgcan.dll" set "SDK=C:\Users\allahmoradi\Downloads\zlgcan(261014)\zlgcan_x64"
if not defined SDK if exist "%INST%\payload\zlgcan.dll" (
  echo Using existing installer\payload runtime files
  mkdir "%NATIVE%" 2>nul
  copy /y "%INST%\payload\zlgcan.dll" "%NATIVE%\zlgcan.dll" >nul
  if exist "%INST%\payload\kerneldlls" xcopy /e /i /y "%INST%\payload\kerneldlls" "%NATIVE%\kerneldlls" >nul
  set "SDK=%NATIVE%"
)
if not defined SDK (
  echo ERROR: zlgcan.dll not found.
  echo Copy the x64 ZLG runtime into:
  echo   %NATIVE%
  echo so that folder contains zlgcan.dll and kerneldlls\
  exit /b 1
)
echo SDK: %SDK%

if not exist "%NATIVE%\zlgcan.dll" (
  echo Staging native\x64 ...
  mkdir "%NATIVE%" 2>nul
  copy /y "%SDK%\zlgcan.dll" "%NATIVE%\zlgcan.dll" >nul
  if exist "%SDK%\kerneldlls" xcopy /e /i /y "%SDK%\kerneldlls" "%NATIVE%\kerneldlls" >nul
)

set "APPEXE="
if exist "%ROOT%\USBCANTool\bin\x64\Release\USBCANTool.exe" set "APPEXE=%ROOT%\USBCANTool\bin\x64\Release\USBCANTool.exe"
if not defined APPEXE if exist "%ROOT%\USBCANTool\bin\Release\USBCANTool.exe" set "APPEXE=%ROOT%\USBCANTool\bin\Release\USBCANTool.exe"
if not defined APPEXE if exist "%ROOT%\USBCANTool\bin\x64\Debug\USBCANTool.exe" set "APPEXE=%ROOT%\USBCANTool\bin\x64\Debug\USBCANTool.exe"
if not defined APPEXE if exist "%ROOT%\USBCANTool\bin\Debug\USBCANTool.exe" set "APPEXE=%ROOT%\USBCANTool\bin\Debug\USBCANTool.exe"
if not defined APPEXE (
  echo ERROR: USBCANTool.exe not found.
  echo In Visual Studio: set platform x64, Build the USBCANTool project, then run this bat again.
  echo Or open the solution and build the USBCANToolSetup project.
  exit /b 1
)
echo App: %APPEXE%
for %%I in ("%APPEXE%") do set "APPDIR=%%~dpI"

echo Preparing payload ...
if exist "%PAYLOAD%" rmdir /s /q "%PAYLOAD%"
mkdir "%PAYLOAD%"
copy /y "%APPEXE%" "%PAYLOAD%\USBCANTool.exe" >nul
if exist "%APPDIR%USBCANTool.exe.config" (
  copy /y "%APPDIR%USBCANTool.exe.config" "%PAYLOAD%\USBCANTool.exe.config" >nul
) else if exist "%ROOT%\USBCANTool\App.config" (
  copy /y "%ROOT%\USBCANTool\App.config" "%PAYLOAD%\USBCANTool.exe.config" >nul
)
copy /y "%SDK%\zlgcan.dll" "%PAYLOAD%\zlgcan.dll" >nul
if not exist "%SDK%\kerneldlls" (
  echo ERROR: kerneldlls folder missing in %SDK%
  exit /b 1
)
xcopy /e /i /y "%SDK%\kerneldlls" "%PAYLOAD%\kerneldlls" >nul

if exist "%ZIP%" del /f /q "%ZIP%"
"%CSC%" /nologo /target:exe /out:"%INST%\ZipMaker.exe" /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll "%INST%\ZipMaker.cs"
if errorlevel 1 exit /b 1
"%INST%\ZipMaker.exe" "%PAYLOAD%" "%ZIP%"
if errorlevel 1 exit /b 1

if not exist "%DIST%" mkdir "%DIST%"
echo Compiling dist\USBCANToolSetup.exe ...
pushd "%INST%"
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /win32manifest:app.manifest /resource:payload.zip,payload.zip /out:"%DIST%\USBCANToolSetup.exe" /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.dll" /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll" /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll" /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.dll" /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\System.IO.Compression.FileSystem.dll" Installer.cs
set "ERR=%ERRORLEVEL%"
popd
if not "%ERR%"=="0" exit /b %ERR%

echo.
echo Done: %DIST%\USBCANToolSetup.exe
echo Copy that file to the other PC and double-click it. No admin, no PowerShell.
exit /b 0
