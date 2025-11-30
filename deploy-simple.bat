@echo off
chcp 65001 >nul
echo ========================================
echo   部署 RimTalk-ExpandMemory v3.3.2.3
echo   (异步向量化版本)
echo ========================================
echo.

set "TARGET=D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory"

echo [1/3] 复制DLL文件...
xcopy /Y /Q "1.6\Assemblies\*.dll" "%TARGET%\1.6\Assemblies\"
echo ? DLL复制完成

echo.
echo [2/3] 复制About文件...
xcopy /Y /Q "About\*.*" "%TARGET%\About\"
echo ? About复制完成

echo.
echo [3/3] 清理旧的SQLite依赖...
REM Microsoft.Data.Sqlite不需要本地DLL
if exist "%TARGET%\1.6\Assemblies\x86" (
    rd /S /Q "%TARGET%\1.6\Assemblies\x86"
    echo ? x86文件夹已删除
)
if exist "%TARGET%\1.6\Assemblies\x64" (
    rd /S /Q "%TARGET%\1.6\Assemblies\x64"
    echo ? x64文件夹已删除
)
if exist "%TARGET%\1.6\Assemblies\SQLite.Interop.dll" (
    del /F /Q "%TARGET%\1.6\Assemblies\SQLite.Interop.dll"
    echo ? SQLite.Interop.dll已删除
)
if exist "%TARGET%\1.6\Assemblies\System.Data.SQLite.dll" (
    del /F /Q "%TARGET%\1.6\Assemblies\System.Data.SQLite.dll"
    echo ? System.Data.SQLite.dll已删除
)

echo.
echo ========================================
echo   部署完成！v3.3.2.3
echo ========================================
echo.
echo 文件位置: %TARGET%
echo.
echo ? 新功能 v3.3.2.3:
echo - ?? 异步向量化同步（自动升级哈希→语义向量）
echo - ?? 手动注入后台异步升级
echo - ?? 智能用户提示（告知升级状态）
echo - ? 零UI卡顿（完全后台处理）
echo.
echo ?? 现在可以启动RimWorld测试！
echo.
