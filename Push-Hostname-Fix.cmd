@echo off
setlocal

git --version >nul 2>&1
if errorlevel 1 (
  echo Git is not installed or not on PATH.
  echo Install Git, then run this script again.
  pause
  exit /b 1
)

git init
git branch -M main
git remote get-url origin >nul 2>&1
if errorlevel 1 (
  git remote add origin https://github.com/IamN8Wright/N8s-IPScanner.git
) else (
  git remote set-url origin https://github.com/IamN8Wright/N8s-IPScanner.git
)

git add N8sIPScanner/IpScanner.cs N8sIPScanner/N8sIPScanner.csproj N8sIPScanner/GitHubUpdateService.cs README.md ReleaseNotes-v2.3.4.txt Push-Hostname-Fix.cmd Tag-Release-v2.3.4.cmd
git commit -m "Release v2.3.4: hostname enrichment fixes"
if errorlevel 1 (
  echo No commit was created. This usually means there were no file changes.
)

git pull origin main --allow-unrelated-histories --no-rebase -X ours --no-edit
git push -u origin main

echo.
echo Main branch pushed. To create the GitHub release EXE, run:
echo Tag-Release-v2.3.4.cmd
echo.
pause
