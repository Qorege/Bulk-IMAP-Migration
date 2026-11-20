@echo off
setlocal

echo ============================================================
echo  Building IMAPMigrator.WPF.exe (Material Design 3 - .NET 8)
echo ============================================================

where dotnet >nul 2>nul
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET SDK (dotnet) not found in PATH.
    echo Please install .NET 8 SDK or higher.
    pause
    exit /b 1
)

echo Publishing standalone single-file WPF edition...
dotnet publish "%~dp0IMAPMigrator.WPF\IMAPMigrator.WPF.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%~dp0bin_wpf"

if %ERRORLEVEL% EQU 0 (
    copy /y "%~dp0bin_wpf\IMAPMigrator.WPF.exe" "%~dp0IMAPMigrator.WPF.exe" >nul
    echo.
    echo [SUCCESS] IMAPMigrator.WPF.exe compiled and placed in root directory!
) else (
    echo.
    echo [ERROR] Build failed.
)

pause
