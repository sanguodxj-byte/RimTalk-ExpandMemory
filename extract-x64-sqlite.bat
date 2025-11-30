@echo off
chcp 65001 >nul
echo ========================================
echo   提取 x64 SQLite.Interop.dll
echo ========================================
echo.

REM 解压NuGet包
echo [1/3] 解压sqlite.zip...
powershell -Command "Expand-Archive -Path 'temp\sqlite.zip' -DestinationPath 'temp\sqlite' -Force"
echo ? 解压完成

echo.
echo [2/3] 提取x64版本...
if not exist "1.6\Assemblies\x64" mkdir "1.6\Assemblies\x64"
copy /Y "temp\sqlite\build\net46\x64\SQLite.Interop.dll" "1.6\Assemblies\x64\SQLite.Interop.dll"
echo ? x64版本已复制到项目

echo.
echo [3/3] 清理临时文件...
rd /S /Q temp
echo ? 临时文件已清理

echo.
echo ========================================
echo   提取完成！
echo ========================================
echo.
echo x64 SQLite.Interop.dll 已保存到:
echo 1.6\Assemblies\x64\SQLite.Interop.dll
echo.
echo 下一步: 运行 deploy-simple.bat 部署到游戏
echo.
pause
