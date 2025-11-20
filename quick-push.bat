@echo off
chcp 65001 >nul
setlocal

:: =================================================
:: Quick Git Push
:: =================================================
:: Quick commit with default message and push
:: Usage: quick-push.bat [optional custom message]
:: =================================================

echo.
echo [QUICK PUSH] Starting...
echo.

:: Add all changes
git add -A

:: Check for changes
git diff-index --quiet HEAD --
if %ERRORLEVEL% EQU 0 (
    echo [INFO] No changes to commit
    goto :end
)

:: Use provided message or default
if "%~1"=="" (
    set "MSG=refactor: Quick update"
) else (
    set "MSG=%~1"
)

echo [COMMIT] %MSG%
git commit -m "%MSG%"

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Commit failed!
    goto :end
)

:: Get current branch and push
for /f "tokens=*" %%b in ('git branch --show-current') do set BRANCH=%%b
if "%BRANCH%"=="" set "BRANCH=main"

echo [PUSH] Pushing to %BRANCH%...
git push origin %BRANCH%

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Push failed!
    goto :end
)

echo.
echo [SUCCESS] ✓ Quick push completed!

:end
echo.
if "%~1"=="" pause
