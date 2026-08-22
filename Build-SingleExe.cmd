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
if exist "%~dp0dist-single" rmdir /s /q "%~dp0dist-single"

echo.
echo Restoring runtime packs for win-x64...
dotnet restore "N8sIPScanner.csproj" -r win-x64 --source https://api.nuget.org/v3/index.json
if errorlevel 1 goto fail

echo.
echo Publishing single-file self-contained EXE...
dotnet publish "N8sIPScanner.csproj" ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  --no-restore ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None ^
  -p:DebugSymbols=false ^
  -o "%~dp0dist-single"

if errorlevel 1 goto fail

echo.
echo Removing non-EXE publish artifacts, if any...
for %%F in ("%~dp0dist-single\*") do (
    if /I not "%%~nxF"=="N8s-IPScanner.exe" del /q "%%~fF" >nul 2>nul
)
for /d %%D in ("%~dp0dist-single\*") do rmdir /s /q "%%~fD" >nul 2>nul

echo.
echo Done.
echo Single EXE created here:
echo %~dp0dist-single\N8s-IPScanner.exe
echo.
echo This version opens normally and only asks for UAC when applying NIC changes.
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
