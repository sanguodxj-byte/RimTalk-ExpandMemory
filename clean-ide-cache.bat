@echo off
chcp 65001 > nul
echo ========================================
echo ?? 清理IDE缓存
echo ========================================
echo.

echo ?? 关闭Visual Studio...
taskkill /F /IM devenv.exe 2>nul
timeout /t 2 /nobreak > nul

echo ??? 删除.vs缓存文件夹...
if exist ".vs" (
    rmdir /s /q ".vs"
    echo   ? .vs文件夹已删除
) else (
    echo   ?? .vs文件夹不存在
)

echo ??? 删除bin/obj文件夹...
if exist "bin" rmdir /s /q "bin"
if exist "obj" rmdir /s /q "obj"
echo   ? bin/obj文件夹已删除

echo.
echo ========================================
echo ? 缓存清理完成！
echo ========================================
echo.
echo ?? 下一步：
echo   1. 重新打开RimTalk-ExpandMemory.sln
echo   2. 查看CommonKnowledgeLibrary.cs（应该没有重复代码）
echo   3. 重新编译并部署
echo.
pause
