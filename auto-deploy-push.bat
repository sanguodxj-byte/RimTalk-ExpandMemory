@echo off
chcp 65001 >nul
setlocal

:: =================================================
:: RimTalk-Expand Memory - Auto Deploy + Push Script
:: =================================================
:: This script runs fully automated without user input:
::   1. Deploys the mod to RimWorld (if game not running)
::   2. Automatically commits with timestamp
::   3. Pushes to Git repository
:: 
:: Perfect for:
::   - Post-build automation
::   - Scheduled tasks
::   - CI/CD workflows
:: =================================================

set "LOG_FILE=%~dp0auto-deploy.log"

:: Start logging
echo ============================================ >> "%LOG_FILE%"
echo Auto Deploy Started: %DATE% %TIME% >> "%LOG_FILE%"
echo ============================================ >> "%LOG_FILE%"

echo.
echo [AUTO] Starting automated deployment and push...
echo.

:: --- Configuration ---
set "PROJECT_DIR=%~dp0"
set "DEST_DIR=D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory"
set "GAME_PROCESS=RimWorldWin64.exe"

:: Step 1: Check if RimWorld is running
echo [CHECK] Checking if RimWorld is running...
tasklist /FI "IMAGENAME eq %GAME_PROCESS%" 2>NUL | find /I /N "%GAME_PROCESS%" >NUL
if "%ERRORLEVEL%"=="0" (
    echo [SKIP] RimWorld is running, skipping deployment | tee -a "%LOG_FILE%"
    echo [INFO] Will only perform Git operations | tee -a "%LOG_FILE%"
    set "DEPLOY_SKIPPED=1"
) else (
    echo [OK] RimWorld is not running | tee -a "%LOG_FILE%"
    set "DEPLOY_SKIPPED=0"
)

echo.

:: Step 2: Deploy if game not running
if "%DEPLOY_SKIPPED%"=="0" (
    echo [DEPLOY] Deploying mod files...
    
    :: Deploy version 1.5
    if exist "%PROJECT_DIR%1.5\Assemblies\RimTalkMemoryPatch.dll" (
        if not exist "%DEST_DIR%\1.5\Assemblies\" mkdir "%DEST_DIR%\1.5\Assemblies"
        copy /Y "%PROJECT_DIR%1.5\Assemblies\RimTalkMemoryPatch.dll" "%DEST_DIR%\1.5\Assemblies\" >NUL
        copy /Y "%PROJECT_DIR%1.5\Assemblies\RimTalkMemoryPatch.pdb" "%DEST_DIR%\1.5\Assemblies\" 2>NUL
        echo [OK] Version 1.5 deployed | tee -a "%LOG_FILE%"
    )
    
    :: Deploy version 1.6
    if exist "%PROJECT_DIR%1.6\Assemblies\RimTalkMemoryPatch.dll" (
        if not exist "%DEST_DIR%\1.6\Assemblies\" mkdir "%DEST_DIR%\1.6\Assemblies"
        copy /Y "%PROJECT_DIR%1.6\Assemblies\RimTalkMemoryPatch.dll" "%DEST_DIR%\1.6\Assemblies\" >NUL
        copy /Y "%PROJECT_DIR%1.6\Assemblies\RimTalkMemoryPatch.pdb" "%DEST_DIR%\1.6\Assemblies\" 2>NUL
        echo [OK] Version 1.6 deployed | tee -a "%LOG_FILE%"
    )
    
    :: Copy shared files
    xcopy /Y /E /I "%PROJECT_DIR%About" "%DEST_DIR%\About" >NUL 2>&1
    xcopy /Y /E /I "%PROJECT_DIR%Defs" "%DEST_DIR%\Defs" >NUL 2>&1
    xcopy /Y /E /I "%PROJECT_DIR%Languages" "%DEST_DIR%\Languages" >NUL 2>&1
    xcopy /Y /E /I "%PROJECT_DIR%Textures" "%DEST_DIR%\Textures" >NUL 2>&1
    copy /Y "%PROJECT_DIR%LICENSE" "%DEST_DIR%\" >NUL 2>&1
    
    echo [SUCCESS] Deployment completed | tee -a "%LOG_FILE%"
)

echo.

:: Step 3: Git operations
echo [GIT] Starting Git operations...

:: Check for changes
git add -A
git diff-index --quiet HEAD --

if %ERRORLEVEL% EQU 0 (
    echo [INFO] No changes to commit | tee -a "%LOG_FILE%"
    goto :success
)

:: Generate timestamp commit message
for /f "tokens=1-4 delims=/ " %%a in ('date /t') do (set MYDATE=%%a-%%b-%%c)
for /f "tokens=1-2 delims=: " %%a in ('time /t') do (set MYTIME=%%a:%%b)
set "COMMIT_MSG=chore: Auto deploy at %MYDATE% %MYTIME%"

echo [GIT] Committing: %COMMIT_MSG% | tee -a "%LOG_FILE%"
git commit -m "%COMMIT_MSG%" >> "%LOG_FILE%" 2>&1

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Commit failed! | tee -a "%LOG_FILE%"
    goto :error
)

:: Get current branch
for /f "tokens=*" %%b in ('git branch --show-current') do set CURRENT_BRANCH=%%b
if "%CURRENT_BRANCH%"=="" set "CURRENT_BRANCH=main"

echo [GIT] Pushing to origin/%CURRENT_BRANCH%... | tee -a "%LOG_FILE%"
git push origin %CURRENT_BRANCH% >> "%LOG_FILE%" 2>&1

if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Push failed! | tee -a "%LOG_FILE%"
    goto :error
)

:success
echo.
echo ============================================
echo [SUCCESS] Auto deployment completed!
echo ============================================
echo Success: %DATE% %TIME% >> "%LOG_FILE%"
echo. >> "%LOG_FILE%"
exit /b 0

:error
echo.
echo ============================================
echo [ERROR] Auto deployment failed!
echo ============================================
echo Error: %DATE% %TIME% >> "%LOG_FILE%"
echo. >> "%LOG_FILE%"
exit /b 1
