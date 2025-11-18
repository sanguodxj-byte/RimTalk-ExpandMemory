@echo off
setlocal

:: =================================================
:: RimTalk Memory Patch - Deployment Script
:: =================================================

:: --- Configuration ---
set "SOURCE_DIR=%~dp01.5\Assemblies"
set "DEST_DIR=D:\steam\steamapps\common\RimWorld\Mods\RimTalk-MemoryPatch\Assemblies"
set "GAME_PROCESS=RimWorldWin64.exe"

:: --- Main Logic ---
echo.
echo [DEPLOY] Starting deployment of RimTalk Memory Patch...
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

:: 2. Check for source files
echo [INFO] Verifying build files in %SOURCE_DIR%...
if not exist "%SOURCE_DIR%\RimTalkMemoryPatch.dll" (
    echo [ERROR] RimTalkMemoryPatch.dll not found. Please build the project first.
    goto :end
)
if not exist "%SOURCE_DIR%\RimTalkMemoryPatch.pdb" (
    echo [WARNING] RimTalkMemoryPatch.pdb not found. Debugging may be affected.
)
echo [SUCCESS] Build files found.
echo.

:: 3. Create destination directory if it doesn't exist
if not exist "%DEST_DIR%\" (
    echo [INFO] Destination directory not found. Creating:
    echo %DEST_DIR%
    mkdir "%DEST_DIR%"
)

:: 4. Copy files
echo [INFO] Copying files to destination...
copy /Y "%SOURCE_DIR%\RimTalkMemoryPatch.dll" "%DEST_DIR%\"
copy /Y "%SOURCE_DIR%\RimTalkMemoryPatch.pdb" "%DEST_DIR%\"

:: 5. Verify copy
if exist "%DEST_DIR%\RimTalkMemoryPatch.dll" (
    echo [SUCCESS] Deployment complete!
) else (
    echo [ERROR] Failed to copy files. Please check permissions.
)

:end
echo.
pause
