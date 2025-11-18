@echo off
setlocal

:: =================================================
:: RimTalk-Expand Memory - Deployment Script
:: =================================================

:: --- Configuration ---
set "PROJECT_DIR=%~dp0"
set "DEST_DIR=D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory"
set "GAME_PROCESS=RimWorldWin64.exe"

:: --- Main Logic ---
echo.
echo [DEPLOY] Starting deployment of RimTalk-Expand Memory...
echo.

:: 1. Check if RimWorld is running
echo [INFO] Checking if %GAME_PROCESS% is running...
tasklist /FI "IMAGENAME eq %GAME_PROCESS%" 2>NUL | find /I /N "%GAME_PROCESS%" >NUL
if "%ERRORLEVEL%"=="0" (
    echo [ERROR] RimWorld is currently running!
    echo Please close the game completely before deploying the mod.
    goto :end
)
echo [SUCCESS] RimWorld is not running.
echo.

:: 2. Deploy version 1.5
echo [INFO] Deploying version 1.5...
if exist "%PROJECT_DIR%1.5\Assemblies\RimTalkMemoryPatch.dll" (
    if not exist "%DEST_DIR%\1.5\Assemblies\" mkdir "%DEST_DIR%\1.5\Assemblies"
    copy /Y "%PROJECT_DIR%1.5\Assemblies\RimTalkMemoryPatch.dll" "%DEST_DIR%\1.5\Assemblies\"
    copy /Y "%PROJECT_DIR%1.5\Assemblies\RimTalkMemoryPatch.pdb" "%DEST_DIR%\1.5\Assemblies\" 2>NUL
    echo [SUCCESS] Version 1.5 deployed
) else (
    echo [WARNING] Version 1.5 not found, skipping
)

echo.

:: 3. Deploy version 1.6
echo [INFO] Deploying version 1.6...
if exist "%PROJECT_DIR%1.6\Assemblies\RimTalkMemoryPatch.dll" (
    if not exist "%DEST_DIR%\1.6\Assemblies\" mkdir "%DEST_DIR%\1.6\Assemblies"
    copy /Y "%PROJECT_DIR%1.6\Assemblies\RimTalkMemoryPatch.dll" "%DEST_DIR%\1.6\Assemblies\"
    copy /Y "%PROJECT_DIR%1.6\Assemblies\RimTalkMemoryPatch.pdb" "%DEST_DIR%\1.6\Assemblies\" 2>NUL
    echo [SUCCESS] Version 1.6 deployed
) else (
    echo [WARNING] Version 1.6 not found, skipping
)

echo.

:: 4. Copy non-version-specific files
echo [INFO] Copying shared files (About, Defs, Languages, etc.)...
xcopy /Y /E /I "%PROJECT_DIR%About" "%DEST_DIR%\About" >NUL
xcopy /Y /E /I "%PROJECT_DIR%Defs" "%DEST_DIR%\Defs" >NUL
xcopy /Y /E /I "%PROJECT_DIR%Languages" "%DEST_DIR%\Languages" >NUL
xcopy /Y /E /I "%PROJECT_DIR%Textures" "%DEST_DIR%\Textures" >NUL
copy /Y "%PROJECT_DIR%LICENSE" "%DEST_DIR%\" 2>NUL

echo [SUCCESS] Shared files deployed
echo.

:: 5. Verify deployment
set "DEPLOY_OK=1"
if not exist "%DEST_DIR%\1.5\Assemblies\RimTalkMemoryPatch.dll" (
    if not exist "%DEST_DIR%\1.6\Assemblies\RimTalkMemoryPatch.dll" (
        echo [ERROR] No valid deployment found!
        set "DEPLOY_OK=0"
    )
)

if "%DEPLOY_OK%"=="1" (
    echo [SUCCESS] ===================================
    echo [SUCCESS] Deployment complete!
    echo [SUCCESS] ===================================
    echo.
    echo Deployed to: %DEST_DIR%
) else (
    echo [ERROR] Deployment failed. Please check the build output.
)

:end
echo.
pause
