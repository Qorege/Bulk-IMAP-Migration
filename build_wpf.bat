@echo off
setlocal

echo ============================================================
echo  Building Bulk IMAP Migration Suite (WPF MVVM Edition)
echo  UI: WPF + MVVM (CommunityToolkit.Mvvm, MahApps.Metro,
echo                  MaterialDesignInXaml, LiveCharts2, WPF UI)
echo ============================================================

dotnet --version >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] .NET SDK is not found on PATH.
    echo Please install .NET 8 SDK or higher from https://dotnet.microsoft.com/download
    pause
    exit /b 1
)

echo Publishing Standalone Single-File Release...
dotnet publish "IMAPMigrator.WPF\IMAPMigrator.WPF.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "bin_wpf" --nologo

if %ERRORLEVEL% EQU 0 (
    copy /y "bin_wpf\IMAPMigrator.WPF.exe" "IMAPMigrator.WPF.exe" >nul
    echo.
    echo ============================================================
    echo [SUCCESS] WPF MVVM application published successfully!
    echo Standalone executable: IMAPMigrator.WPF.exe
    echo Output directory: bin_wpf\
    echo ============================================================
) else (
    echo.
    echo [ERROR] Build failed. Please check errors above.
)

pause
