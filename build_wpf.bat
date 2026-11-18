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

echo Building Release...
dotnet publish "IMAPMigrator.WPF\IMAPMigrator.WPF.csproj" -c Release -o "bin_wpf" --nologo

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ============================================================
    echo [SUCCESS] WPF MVVM application published successfully!
    echo Output directory: bin_wpf\
    echo Executable: bin_wpf\IMAPMigrator.WPF.exe
    echo ============================================================
) else (
    echo.
    echo [ERROR] Build failed. Please check errors above.
)

pause
