@echo off
chcp 65001 >nul
setlocal

:: =================================================
:: RimTalk-Expand Memory - Deploy + Git Push Script
:: =================================================
:: This script:
::   1. Deploys the mod to RimWorld
::   2. Automatically commits and pushes to Git
:: =================================================

echo.
echo ========================================
echo RimTalk Memory Patch - Deploy and Push
echo ========================================
echo.

:: Step 1: Deploy the mod
echo [STEP 1/2] Deploying mod to RimWorld...
echo.
call deploy.bat

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Deployment failed! Aborting Git operations.
    goto :end
)

echo.
echo ========================================
echo.

:: Step 2: Git commit and push
echo [STEP 2/2] Committing and pushing to Git...
echo.

set /p DO_PUSH="Do you want to commit and push changes to Git? (Y/N): "
if /I not "%DO_PUSH%"=="Y" (
    echo [INFO] Skipping Git operations.
    goto :end
)

echo.
call git-push.bat

echo.
echo ========================================
echo [SUCCESS] All operations completed!
echo ========================================

:end
echo.
pause
