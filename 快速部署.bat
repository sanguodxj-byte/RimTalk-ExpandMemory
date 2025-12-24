@echo off
chcp 65001 >nul
echo ========================================
echo RimTalk-ExpandMemory 快速部署
echo ========================================
echo.

set "TARGET=D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory"

echo 正在编译项目...
dotnet build RimTalk-ExpandMemory.csproj --configuration Release
if errorlevel 1 (
    echo 编译失败！
    pause
    exit /b 1
)
echo ? 编译成功
echo.

echo 正在部署文件到: %TARGET%
echo.

if not exist "%TARGET%" mkdir "%TARGET%"

echo 复制 About 文件夹...
robocopy "About" "%TARGET%\About" /MIR /NFL /NDL /NJH /NJS /NP

echo 复制 Defs 文件夹...
if exist "Defs" robocopy "Defs" "%TARGET%\Defs" /MIR /NFL /NDL /NJH /NJS /NP

echo 复制 Languages 文件夹...
if exist "Languages" robocopy "Languages" "%TARGET%\Languages" /MIR /NFL /NDL /NJH /NJS /NP

echo 复制 Textures 文件夹...
if exist "Textures" robocopy "Textures" "%TARGET%\Textures" /MIR /NFL /NDL /NJH /NJS /NP

echo 复制 1.5 版本文件...
if exist "1.5" robocopy "1.5" "%TARGET%\1.5" /MIR /NFL /NDL /NJH /NJS /NP

echo 复制 1.6 版本文件...
if exist "1.6" robocopy "1.6" "%TARGET%\1.6" /MIR /NFL /NDL /NJH /NJS /NP

echo.
echo ========================================
echo ? 部署完成！
echo ========================================
echo.
echo Mod 已部署到: %TARGET%
echo.
pause
