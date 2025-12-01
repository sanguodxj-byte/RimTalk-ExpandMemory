@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

echo ========================================
echo   ?? RimTalk-ExpandMemory v3.3.2.4
echo   (纯C#内存存储 - 零Native依赖)
echo ========================================
echo.

REM 设置目标路径
set "TARGET=D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory"

REM 检查目标路径是否存在
if not exist "%TARGET%" (
    echo ? 错误: 找不到目标路径
    echo    %TARGET%
    pause
    exit /b 1
)

echo [1/4] 编译项目...
dotnet build -c Release /p:BuildingWithScript=true /p:OutputPath=bin_deploy
if errorlevel 1 (
    echo ? 编译失败
    pause
    exit /b 1
)
echo ? 编译完成
echo.

echo [2/4] 复制主DLL（不包含SQLite库）...
xcopy /Y /Q "bin_deploy\RimTalkMemoryPatch.dll" "%TARGET%\1.6\Assemblies\"
echo ? 主DLL复制完成
echo.

echo [3/4] 清理旧的SQLite相关文件（v3.3.2.4不再需要）...
if exist "%TARGET%\1.6\Assemblies\e_sqlite3.dll" (
    del /Q "%TARGET%\1.6\Assemblies\e_sqlite3.dll"
    echo ? 已删除 e_sqlite3.dll
)

if exist "%TARGET%\1.6\Assemblies\Microsoft.Data.Sqlite.dll" (
    del /Q "%TARGET%\1.6\Assemblies\Microsoft.Data.Sqlite.dll"
    echo ? 已删除 Microsoft.Data.Sqlite.dll
)

if exist "%TARGET%\1.6\Assemblies\SQLitePCLRaw.*.dll" (
    del /Q "%TARGET%\1.6\Assemblies\SQLitePCLRaw.*.dll"
    echo ? 已删除 SQLitePCLRaw.*.dll
)

if exist "%TARGET%\1.6\Assemblies\System.Buffers.dll" (
    del /Q "%TARGET%\1.6\Assemblies\System.Buffers.dll"
    echo ? 已删除 System.Buffers.dll
)

if exist "%TARGET%\1.6\Assemblies\System.Memory.dll" (
    del /Q "%TARGET%\1.6\Assemblies\System.Memory.dll"
    echo ? 已删除 System.Memory.dll
)

if exist "%TARGET%\1.6\NativeLibs" (
    rmdir /S /Q "%TARGET%\1.6\NativeLibs"
    echo ? 已删除 NativeLibs 文件夹
)

if exist "%TARGET%\1.6\Assemblies\runtimes" (
    rmdir /S /Q "%TARGET%\1.6\Assemblies\runtimes"
    echo ? 已删除 runtimes 文件夹
)
echo.

echo [4/4] 复制Defs和About文件...
xcopy /Y /E /I /Q "Defs" "%TARGET%\Defs\"
xcopy /Y /E /I /Q "About" "%TARGET%\About\"
echo ? 配置文件复制完成
echo.

echo ========================================
echo   ? 部署完成！v3.3.2.4
echo ========================================
echo.
echo 文件位置: %TARGET%
echo.
echo ?? 部署内容:
echo   ? 主DLL: RimTalkMemoryPatch.dll
echo   ? 配置文件: Defs + About
echo   ? 已清理: 所有SQLite相关文件
echo.
echo ?? v3.3.2.4 关键特性:
echo   - ? 零Native依赖（纯C#实现）
echo   - ? InMemoryVectorStore（SIMD加速）
echo   - ? 滑动窗口（自动清理10k+向量）
echo   - ? 记忆衰减（智能保留热门记忆）
echo   - ? 100%%跨平台兼容（Win/Mac/Linux）
echo.
echo ?? 现在可以启动RimWorld测试！
echo.
pause
