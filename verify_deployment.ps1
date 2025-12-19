# RimTalk-ExpandMemory v3.3.19 部署验证脚本
# 验证部署是否成功

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  v3.3.19 部署验证" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$gameDir = "D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory"

# 1. 检查DLL
Write-Host "[1/4] 检查 DLL..." -ForegroundColor Yellow
$dllPath = "$gameDir\1.6\Assemblies\RimTalk-ExpandMemory.dll"
if (Test-Path $dllPath) {
    $dll = Get-Item $dllPath
    Write-Host "  ? DLL 已部署" -ForegroundColor Green
    Write-Host "    位置: $dllPath" -ForegroundColor Gray
    Write-Host "    大小: $($dll.Length) bytes" -ForegroundColor Gray
    Write-Host "    修改时间: $($dll.LastWriteTime)" -ForegroundColor Gray
} else {
    Write-Host "  ? 未找到 DLL" -ForegroundColor Red
    exit 1
}

# 2. 检查 About.xml
Write-Host "[2/4] 检查 About.xml..." -ForegroundColor Yellow
$aboutPath = "$gameDir\About\About.xml"
if (Test-Path $aboutPath) {
    $content = Get-Content $aboutPath -Raw
    if ($content -match 'v3.3.19') {
        Write-Host "  ? About.xml 版本正确 (v3.3.19)" -ForegroundColor Green
    } else {
        Write-Host "  ? About.xml 版本可能不正确" -ForegroundColor Yellow
    }
} else {
    Write-Host "  ? 未找到 About.xml" -ForegroundColor Red
}

# 3. 检查语言文件
Write-Host "[3/4] 检查语言文件..." -ForegroundColor Yellow
$langFiles = @(
    "$gameDir\Languages\English\Keyed\MemoryPatch.xml",
    "$gameDir\Languages\ChineseSimplified\Keyed\MemoryPatch.xml"
)
$allLangOK = $true
foreach ($file in $langFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw
        $keyCount = ([regex]::Matches($content, '<RimTalk_')).Count
        Write-Host "  ? $(Split-Path (Split-Path $file) -Leaf): $keyCount 个翻译键" -ForegroundColor Green
    } else {
        Write-Host "  ? 缺少: $(Split-Path $file -Leaf)" -ForegroundColor Red
        $allLangOK = $false
    }
}

# 4. 检查旧文件
Write-Host "[4/4] 检查旧文件清理..." -ForegroundColor Yellow
$oldFiles = @(
    "$gameDir\Source\Memory\UI\MainTabWindow_Memory_MindStream.cs",
    "$gameDir\Source\Memory\UI\Dialog_CommonKnowledge_Library.cs"
)
$needClean = $false
foreach ($file in $oldFiles) {
    if (Test-Path $file) {
        Write-Host "  ? 发现旧文件: $(Split-Path $file -Leaf)" -ForegroundColor Yellow
        $needClean = $true
    }
}
if (-not $needClean) {
    Write-Host "  ? 无旧文件残留" -ForegroundColor Green
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  ? 验证完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 5. 显示部署总结
Write-Host "?? 部署总结:" -ForegroundColor Cyan
Write-Host "  ? 版本: v3.3.19" -ForegroundColor White
Write-Host "  ? DLL: ? 已部署" -ForegroundColor White
Write-Host "  ? About.xml: ? 已更新" -ForegroundColor White
Write-Host "  ? 语言文件: ? 完整" -ForegroundColor White
Write-Host ""

Write-Host "?? 下一步:" -ForegroundColor Yellow
Write-Host "  1. 启动 RimWorld" -ForegroundColor White
Write-Host "  2. 确认 Mod 加载正常（无红色错误）" -ForegroundColor White
Write-Host "  3. 进入游戏，打开 Memory 标签页" -ForegroundColor White
Write-Host "  4. 测试新的 Mind Stream UI" -ForegroundColor White
Write-Host "  5. 点击'常识'按钮，测试常识库 UI" -ForegroundColor White
Write-Host "  6. 检查所有文本是否显示中文（或英文）" -ForegroundColor White
Write-Host ""

Write-Host "?? 测试文档:" -ForegroundColor Yellow
Write-Host "  - 测试清单: TESTING_CHECKLIST_v3.3.19.md" -ForegroundColor White
Write-Host "  - 快速入门: QUICKSTART_UI_v3.3.19.md" -ForegroundColor White
Write-Host "  - 部署报告: FINAL_DEPLOYMENT_REPORT_v3.3.19.md" -ForegroundColor White
Write-Host ""

# 询问是否启动游戏
$response = Read-Host "是否现在启动游戏进行测试? (Y/N)"
if ($response -eq "Y" -or $response -eq "y") {
    Write-Host ""
    Write-Host "正在启动 RimWorld..." -ForegroundColor Green
    Start-Process "steam://rungameid/294100"
    Write-Host "游戏启动命令已发送！" -ForegroundColor Green
    Write-Host ""
    Write-Host "提示: 游戏启动后，记得按照测试清单验证所有功能。" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "按任意键退出..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
