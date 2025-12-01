@echo off
chcp 65001 >nul
echo ========================================
echo   部署 RimTalk-ExpandMemory v3.3.2.3
echo   (异步向量化版本 + SQLite修复)
echo ========================================
echo.

set "TARGET=D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory"

echo [1/5] 复制DLL文件...
xcopy /Y /Q "1.6\Assemblies\*.dll" "%TARGET%\1.6\Assemblies\"
echo ? DLL复制完成

echo.
echo [2/5] 复制SQLite本地库...
if exist "1.6\Assemblies\runtimes" (
    REM ? 只复制x64和x86版本，跳过ARM
    if exist "1.6\Assemblies\runtimes\win-x64" (
        xcopy /Y /E /I /Q "1.6\Assemblies\runtimes\win-x64" "%TARGET%\1.6\Assemblies\runtimes\win-x64\"
        echo ? SQLite x64库复制完成
    )
    if exist "1.6\Assemblies\runtimes\win-x86" (
        xcopy /Y /E /I /Q "1.6\Assemblies\runtimes\win-x86" "%TARGET%\1.6\Assemblies\runtimes\win-x86\"
        echo ? SQLite x86库复制完成
    )
) else (
    echo ? 警告：找不到SQLite runtimes文件夹
)

echo.
echo [3/5] 复制Defs文件...
if exist "Defs" (
    xcopy /Y /E /I /Q "Defs\*.*" "%TARGET%\Defs\"
    echo ? Defs复制完成
) else (
    echo ? 警告：找不到Defs文件夹
)

echo.
echo [4/5] 复制About文件...
xcopy /Y /Q "About\*.*" "%TARGET%\About\"
echo ? About复制完成

echo.
echo [5/5] 清理旧的SQLite依赖...
REM Microsoft.Data.Sqlite不需要这些旧文件
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
echo ? 修复内容 v3.3.2.3:
echo - ?? SQLite初始化错误修复（添加本地e_sqlite3.dll）
echo - ?? MainTabDef缺失修复（添加Defs/MainTabDefs.xml）
echo - ?? 异步向量化同步（自动升级哈希→语义向量）
echo - ?? 手动注入后台异步升级
echo - ?? 智能用户提示（告知升级状态）
echo - ? 零UI卡顿（完全后台处理）
echo.
echo ?? 现在可以启动RimWorld测试！
echo.
