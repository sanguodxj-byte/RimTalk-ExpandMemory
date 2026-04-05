@echo off
rem The original script used 'chcp 65001' to support Chinese characters.
rem This is no longer necessary as the script is now in English.
echo ========================================
echo RimTalk-ExpandMemory Quick Deploy
echo ========================================
echo.

set "TARGET=D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory"

echo Compiling project...
dotnet build RimTalk-ExpandMemory.csproj --configuration Release -p:RimTalkDir="D:\steam\steamapps\workshop\content\294100\3551203752"
if errorlevel 1 (
    echo Build failed!
    pause
    exit /b 1
)
echo.
echo Build successful.
echo.

echo Deploying files to: %TARGET%
echo.

if not exist "%TARGET%" mkdir "%TARGET%"

echo Copying About folder...
robocopy "About" "%TARGET%\About" /MIR /NFL /NDL /NJH /NJS /NP

echo Copying Defs folder...
if exist "Defs" robocopy "Defs" "%TARGET%\Defs" /MIR /NFL /NDL /NJH /NJS /NP

echo Copying Languages folder...
if exist "Languages" robocopy "Languages" "%TARGET%\Languages" /MIR /NFL /NDL /NJH /NJS /NP

echo Copying Textures folder...
if exist "Textures" robocopy "Textures" "%TARGET%\Textures" /MIR /NFL /NDL /NJH /NJS /NP

echo Copying 1.5 version files...
if exist "1.5" robocopy "1.5" "%TARGET%\1.5" /MIR /NFL /NDL /NJH /NJS /NP

echo Copying 1.6 version files...
if exist "1.6" robocopy "1.6" "%TARGET%\1.6" /MIR /NFL /NDL /NJH /NJS /NP

echo.
echo ========================================
echo Deployment complete!
echo ========================================
echo.
echo Mod has been deployed to: %TARGET%
echo.
pause
