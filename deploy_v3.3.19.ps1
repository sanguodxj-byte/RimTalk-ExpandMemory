# RimTalk-ExpandMemory v3.3.19 部署脚本
# 最后更新: 2025-01-XX

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RimTalk-ExpandMemory v3.3.19 部署" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 1. 检查游戏目录
$gameDir = "D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory"
Write-Host "[1/6] 检查游戏目录..." -ForegroundColor Yellow
if (Test-Path $gameDir) {
    Write-Host "  ? 找到游戏目录: $gameDir" -ForegroundColor Green
} else {
    Write-Host "  ? 未找到游戏目录，请手动部署" -ForegroundColor Red
    exit 1
}

# 2. 检查编译输出
Write-Host "[2/6] 检查编译输出..." -ForegroundColor Yellow
$dllPath = "1.6\Assemblies\RimTalk-ExpandMemory.dll"
if (Test-Path $dllPath) {
    Write-Host "  ? 找到编译后的DLL" -ForegroundColor Green
} else {
    Write-Host "  ? 未找到DLL，请先编译项目" -ForegroundColor Red
    Write-Host "  提示: 检查路径 $dllPath" -ForegroundColor Gray
    exit 1
}

# 3. 备份现有文件
Write-Host "[3/6] 备份现有文件..." -ForegroundColor Yellow
$backupDir = "$gameDir\_backup_v3.3.19"
if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir | Out-Null
}
if (Test-Path "$gameDir\1.6\Assemblies\RimTalk-ExpandMemory.dll") {
    Copy-Item "$gameDir\1.6\Assemblies\RimTalk-ExpandMemory.dll" "$backupDir\RimTalk-ExpandMemory.dll.bak"
    Write-Host "  ? 已备份旧DLL" -ForegroundColor Green
}

# 4. 部署文件
Write-Host "[4/6] 部署文件..." -ForegroundColor Yellow

# 部署DLL
Copy-Item $dllPath "$gameDir\1.6\Assemblies\RimTalk-ExpandMemory.dll" -Force
Write-Host "  ? DLL已部署" -ForegroundColor Green

# 部署About.xml
Copy-Item "About\About.xml" "$gameDir\About\About.xml" -Force
Write-Host "  ? About.xml已部署" -ForegroundColor Green

# 部署语言文件
Copy-Item "Languages\English\Keyed\MemoryPatch.xml" "$gameDir\Languages\English\Keyed\MemoryPatch.xml" -Force
Copy-Item "Languages\ChineseSimplified\Keyed\MemoryPatch.xml" "$gameDir\Languages\ChineseSimplified\Keyed\MemoryPatch.xml" -Force
Write-Host "  ? 语言文件已部署" -ForegroundColor Green

# 5. 验证部署
Write-Host "[5/6] 验证部署..." -ForegroundColor Yellow
$deployedDll = "$gameDir\1.6\Assemblies\RimTalk-ExpandMemory.dll"
if (Test-Path $deployedDll) {
    $fileInfo = Get-Item $deployedDll
    Write-Host "  ? DLL大小: $($fileInfo.Length) bytes" -ForegroundColor Green
    Write-Host "  ? 修改时间: $($fileInfo.LastWriteTime)" -ForegroundColor Green
} else {
    Write-Host "  ? 部署失败" -ForegroundColor Red
    exit 1
}

# 6. 清理旧文件
Write-Host "[6/6] 清理旧文件..." -ForegroundColor Yellow
$oldFiles = @(
    "$gameDir\Source\Memory\UI\MainTabWindow_Memory_MindStream.cs",
    "$gameDir\Source\Memory\UI\Dialog_CommonKnowledge_Library.cs"
)
foreach ($file in $oldFiles) {
    if (Test-Path $file) {
        Remove-Item $file -Force
        Write-Host "  ? 已删除: $(Split-Path $file -Leaf)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ? 部署完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "下一步:" -ForegroundColor Yellow
Write-Host "  1. 启动 RimWorld" -ForegroundColor White
Write-Host "  2. 确认 Mod 加载正常" -ForegroundColor White
Write-Host "  3. 测试新UI功能" -ForegroundColor White
Write-Host "  4. 检查翻译是否正确" -ForegroundColor White
Write-Host ""
Write-Host "文档位置:" -ForegroundColor Yellow
Write-Host "  - 快速入门: QUICKSTART_UI_v3.3.19.md" -ForegroundColor White
Write-Host "  - 测试清单: TESTING_CHECKLIST_v3.3.19.md" -ForegroundColor White
Write-Host "  - 更新日志: CHANGELOG.md" -ForegroundColor White
Write-Host ""

# 询问是否打开游戏
$openGame = Read-Host "是否现在启动游戏? (Y/N)"
if ($openGame -eq "Y" -or $openGame -eq "y") {
    Write-Host "正在启动 RimWorld..." -ForegroundColor Yellow
    Start-Process "steam://rungameid/294100"
}

Write-Host "按任意键退出..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
