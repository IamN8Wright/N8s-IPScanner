@echo off
setlocal

git tag v2.3.3
git push origin v2.3.3

echo.
echo Tag pushed. GitHub Actions should now build the release EXE.
echo Check:
echo https://github.com/IamN8Wright/N8s-IPScanner/actions
echo.
pause
