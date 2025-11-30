@echo off
chcp 65001 >nul
echo ========================================
echo   SQLite.Interop.dll 架构检测工具
echo ========================================
echo.

REM 检查x64目录
if exist "1.6\Assemblies\x64\SQLite.Interop.dll" (
    echo [x64目录] 1.6\Assemblies\x64\SQLite.Interop.dll
    powershell -Command "$bytes = [System.IO.File]::ReadAllBytes('1.6\Assemblies\x64\SQLite.Interop.dll'); $arch = if ($bytes[0x13C] -eq 0x64) { '64位 (x64)' } elseif ($bytes[0x13C] -eq 0x4C) { '32位 (x86)' } else { '未知' }; Write-Host '  架构: ' $arch; Write-Host '  大小: ' (Get-Item '1.6\Assemblies\x64\SQLite.Interop.dll').Length 'bytes'"
    echo.
) else (
    echo [x64目录] 文件不存在
    echo.
)

REM 检查x86目录
if exist "1.6\Assemblies\x86\SQLite.Interop.dll" (
    echo [x86目录] 1.6\Assemblies\x86\SQLite.Interop.dll
    powershell -Command "$bytes = [System.IO.File]::ReadAllBytes('1.6\Assemblies\x86\SQLite.Interop.dll'); $arch = if ($bytes[0x13C] -eq 0x64) { '64位 (x64)' } elseif ($bytes[0x13C] -eq 0x4C) { '32位 (x86)' } else { '未知' }; Write-Host '  架构: ' $arch; Write-Host '  大小: ' (Get-Item '1.6\Assemblies\x86\SQLite.Interop.dll').Length 'bytes'"
    echo.
) else (
    echo [x86目录] 文件不存在
    echo.
)

REM 检查主目录
if exist "1.6\Assemblies\SQLite.Interop.dll" (
    echo [主目录] 1.6\Assemblies\SQLite.Interop.dll
    powershell -Command "$bytes = [System.IO.File]::ReadAllBytes('1.6\Assemblies\SQLite.Interop.dll'); $arch = if ($bytes[0x13C] -eq 0x64) { '64位 (x64)' } elseif ($bytes[0x13C] -eq 0x4C) { '32位 (x86)' } else { '未知' }; Write-Host '  架构: ' $arch; Write-Host '  大小: ' (Get-Item '1.6\Assemblies\SQLite.Interop.dll').Length 'bytes'"
    echo.
) else (
    echo [主目录] 文件不存在
    echo.
)

echo ========================================
echo   检测完成
echo ========================================
echo.
echo 说明：
echo - RimWorld 64位需要x64版本
echo - RimWorld 32位需要x86版本
echo - 如果架构不匹配会出现BadImageFormatException
echo.
pause
