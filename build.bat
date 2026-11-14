@echo off
setlocal enabledelayedexpansion

echo ============================================================
echo  Building IMAPMigrator.exe (Portable Windows .NET 4.0+)
echo ============================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

if not exist "%CSC%" (
    echo [ERROR] Microsoft .NET Framework 4.0+ compiler not found.
    pause
    exit /b 1
)

echo Using compiler: %CSC%

REM Gather all .cs files from src recursively
set "FILES="
for /r src %%F in (*.cs) do (
    set "FILES=!FILES! "%%F""
)

"%CSC%" /target:winexe /out:IMAPMigrator.exe /win32icon:app.ico /resource:app.ico /resource:icon.png /win32manifest:src\app.manifest /platform:anycpu /optimize+ %FILES%

if %ERRORLEVEL% EQU 0 (
    echo.
    echo [SUCCESS] IMAPMigrator.exe compiled successfully!
) else (
    echo.
    echo [ERROR] Compilation failed.
)

pause
