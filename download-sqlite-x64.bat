@echo off
chcp 65001 >nul
echo ========================================
echo   下载 SQLite.Interop.dll (x64)
echo ========================================
echo.

REM 创建临时目录
if not exist "temp" mkdir temp

echo 正在下载 System.Data.SQLite.Core.1.0.118.0...
powershell -Command "Invoke-WebRequest -Uri 'https://www.nuget.org/api/v2/package/System.Data.SQLite.Core/1.0.118.0' -OutFile 'temp\sqlite.nupkg'"

echo.
echo 正在解压...
powershell -Command "Expand-Archive -Path 'temp\sqlite.nupkg' -DestinationPath 'temp\sqlite' -Force"

echo.
echo 正在复制 x64 版本...
if not exist "1.6\Assemblies\x64" mkdir "1.6\Assemblies\x64"
copy /Y "temp\sqlite\build\net46\x64\SQLite.Interop.dll" "1.6\Assemblies\x64\SQLite.Interop.dll"

echo.
echo 清理临时文件...
rd /S /Q temp

echo.
echo ========================================
echo   下载完成！
echo ========================================
echo.
echo x64 版本已保存到: 1.6\Assemblies\x64\SQLite.Interop.dll
echo.
pause
