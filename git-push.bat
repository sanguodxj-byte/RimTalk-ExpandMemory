@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: =================================================
:: Git Auto Commit & Push Script
:: =================================================
:: Usage: 
::   git-push.bat           - Interactive mode
::   git-push.bat -auto     - Automatic mode with timestamp message
::   git-push.bat "message" - Direct commit with custom message
:: =================================================

set "AUTO_MODE=0"
set "CUSTOM_MSG="

:: Parse arguments
if "%~1"=="-auto" (
    set "AUTO_MODE=1"
) else if not "%~1"=="" (
    set "CUSTOM_MSG=%~1"
)

echo.
echo [GIT] Starting Git operations...
echo.

:: 1. Check git status
echo [INFO] Checking Git status...
git status

echo.
echo [INFO] Adding all changes...
git add -A

:: 2. Check if there are changes to commit
git diff-index --quiet HEAD --
if %ERRORLEVEL% EQU 0 (
    echo [INFO] No changes to commit
    goto :end
)

:: 3. Determine commit message
if "%AUTO_MODE%"=="1" (
    :: Auto mode: generate timestamp message
    for /f "tokens=1-4 delims=/ " %%a in ('date /t') do (set MYDATE=%%a-%%b-%%c)
    for /f "tokens=1-2 delims=: " %%a in ('time /t') do (set MYTIME=%%a:%%b)
    set "COMMIT_MSG=chore: Auto commit at !MYDATE! !MYTIME!"
    echo [AUTO] Using automatic commit message
) else if not "%CUSTOM_MSG%"=="" (
    :: Direct message from argument
    set "COMMIT_MSG=%CUSTOM_MSG%"
    echo [INFO] Using provided commit message
) else (
    :: Interactive mode: prompt for message
    set /p COMMIT_MSG="Enter commit message (or press Enter for default): "
    
    if "!COMMIT_MSG!"=="" (
        set "COMMIT_MSG=refactor: Clean up code and update configurations"
    )
)

echo.
echo [INFO] Committing changes...
echo [INFO] Commit message: !COMMIT_MSG!
git commit -m "!COMMIT_MSG!"

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Commit failed!
    goto :end
)

echo.
echo [SUCCESS] Commit successful!

:: 4. Get current branch name
for /f "tokens=*" %%b in ('git branch --show-current') do set CURRENT_BRANCH=%%b

if "!CURRENT_BRANCH!"=="" (
    echo [WARNING] Could not detect current branch, defaulting to 'main'
    set "CURRENT_BRANCH=main"
)

echo [INFO] Current branch: !CURRENT_BRANCH!

:: 5. Push to remote
echo.
echo [INFO] Pushing to remote repository...
git push origin !CURRENT_BRANCH!

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Push failed on branch '!CURRENT_BRANCH!'
    
    :: Try alternative common branch names
    if "!CURRENT_BRANCH!"=="main" (
        echo [INFO] Trying 'master' branch...
        git push origin master
    ) else if "!CURRENT_BRANCH!"=="master" (
        echo [INFO] Trying 'main' branch...
        git push origin main
    ) else (
        echo [ERROR] Push failed!
        echo Please check your network connection and remote repository settings.
        goto :end
    )
    
    if !ERRORLEVEL! NEQ 0 (
        echo [ERROR] All push attempts failed!
        echo Please check:
        echo   - Network connection
        echo   - Remote repository settings
        echo   - Branch tracking configuration
        goto :end
    )
)

echo.
echo [SUCCESS] ===================================
echo [SUCCESS] Push completed successfully!
echo [SUCCESS] Branch: !CURRENT_BRANCH!
echo [SUCCESS] ===================================

:end
echo.
if "%AUTO_MODE%"=="0" pause
