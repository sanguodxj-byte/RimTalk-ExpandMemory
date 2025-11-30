@echo off
chcp 65001 >nul
echo ========================================
echo   重新提取 x64 SQLite
echo ========================================
echo.

REM 创建临时目录
if exist temp rd /S /Q temp
mkdir temp

echo [1/4] 重新下载NuGet包...
powershell -Command "Invoke-WebRequest -Uri 'https://www.nuget.org/api/v2/package/System.Data.SQLite.Core/1.0.118.0' -OutFile 'temp\sqlite.nupkg'"
if errorlevel 1 (
    echo ? 下载失败
    pause
    exit /b 1
)
echo ? 下载完成

echo.
echo [2/4] 重命名为zip...
ren "temp\sqlite.nupkg" "sqlite.zip"
echo ? 重命名完成

echo.
echo [3/4] 解压并查找x64文件...
powershell -Command "Expand-Archive -Path 'temp\sqlite.zip' -DestinationPath 'temp\extracted' -Force"
echo ? 解压完成

REM 查找x64版本
echo.
echo 查找SQLite.Interop.dll (x64)...
dir /S /B "temp\extracted\*SQLite.Interop.dll"

echo.
echo [4/4] 复制x64版本到项目...
if not exist "1.6\Assemblies\x64" mkdir "1.6\Assemblies\x64"

REM 尝试多个可能的路径
if exist "temp\extracted\build\net46\x64\SQLite.Interop.dll" (
    copy /Y "temp\extracted\build\net46\x64\SQLite.Interop.dll" "1.6\Assemblies\x64\SQLite.Interop.dll"
    echo ? 从build\net46\x64复制成功
) else if exist "temp\extracted\runtimes\win-x64\native\SQLite.Interop.dll" (
    copy /Y "temp\extracted\runtimes\win-x64\native\SQLite.Interop.dll" "1.6\Assemblies\x64\SQLite.Interop.dll"
    echo ? 从runtimes\win-x64\native复制成功
) else (
    echo ? 未找到x64版本
    echo 请手动查看temp\extracted目录
    pause
    exit /b 1
)

echo.
echo 验证文件...
if exist "1.6\Assemblies\x64\SQLite.Interop.dll" (
    echo ? x64版本已成功保存
    dir "1.6\Assemblies\x64\SQLite.Interop.dll"
) else (
    echo ? 文件不存在
)

echo.
echo 清理临时文件...
rd /S /Q temp
echo ? 清理完成

echo.
echo ========================================
echo   提取完成！
echo ========================================
pause
