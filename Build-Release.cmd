@echo off
setlocal
cd /d "%~dp0N8sIPScanner"

where dotnet >nul 2>nul
if errorlevel 1 (
    echo.
    echo .NET SDK was not found.
    echo Install the .NET 8 SDK from Microsoft, then run this again.
    echo.
    pause
    exit /b 1
)

echo.
echo Cleaning old build assets...
if exist obj rmdir /s /q obj
if exist bin rmdir /s /q bin

echo.
echo .NET SDK info:
dotnet --list-sdks
echo.

echo Restoring framework-dependent app with no runtime identifier...
dotnet restore "N8sIPScanner.csproj" --configfile "NuGet.Config" --ignore-failed-sources -v minimal
if errorlevel 1 goto fail

echo.
echo Building release...
dotnet build "N8sIPScanner.csproj" -c Release --no-restore
if errorlevel 1 goto fail

echo.
echo Publishing framework-dependent Windows app...
dotnet publish "N8sIPScanner.csproj" -c Release --no-restore --self-contained false -p:PublishSingleFile=false -o "%~dp0dist"
if errorlevel 1 goto fail

echo.
echo Done.
echo EXE created here:
echo %~dp0dist\N8s-IPScanner.exe
echo.
echo NOTE: This framework-dependent version requires the .NET 8 Desktop Runtime on the target PC.
echo.
pause
exit /b 0

:fail
echo.
echo Build failed.
echo Please copy the FIRST red error above this line.
echo.
pause
exit /b 1
