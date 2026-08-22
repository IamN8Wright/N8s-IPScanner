@echo off
setlocal
cd /d "%~dp0N8s IP Scanner"

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
echo This self-contained build requires NuGet/runtime-pack access.
echo If your network blocks nuget.org, use Build-Release.cmd instead.
echo.

dotnet restore "N8s IP Scanner.csproj" -r win-x64 --ignore-failed-sources -v minimal
if errorlevel 1 goto fail

echo.
echo Publishing self-contained win-x64 app...
dotnet publish "N8s IP Scanner.csproj" -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=false -o "%~dp0dist-self-contained"
if errorlevel 1 goto fail

echo.
echo Done.
echo EXE created here:
echo %~dp0dist-self-contained\N8s IP Scanner.exe
echo.
pause
exit /b 0

:fail
echo.
echo Build failed.
echo Self-contained builds need access to Microsoft runtime packs through NuGet.
echo Try Build-Release.cmd instead, or run this on a machine with NuGet access.
echo.
pause
exit /b 1
