# 快速部署脚本（无参数）
# 编译并部署 Mod 到 RimWorld

Write-Host "正在部署 RimTalk-ExpandMemory..." -ForegroundColor Cyan

# 编译
dotnet build RimTalk-ExpandMemory.csproj --configuration Release --verbosity minimal

if ($LASTEXITCODE -eq 0) {
    Write-Host "? 编译成功" -ForegroundColor Green
    
    # 部署
    $TargetPath = "C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory"
    
    # 创建目录
    New-Item -ItemType Directory -Path $TargetPath -Force | Out-Null
    
    # 复制文件
    Copy-Item -Path "About" -Destination "$TargetPath\About" -Recurse -Force
    Copy-Item -Path "1.6" -Destination "$TargetPath\1.6" -Recurse -Force
    Copy-Item -Path "Languages" -Destination "$TargetPath\Languages" -Recurse -Force
    
    if (Test-Path "Textures") {
        Copy-Item -Path "Textures" -Destination "$TargetPath\Textures" -Recurse -Force
    }
    
    Write-Host "? 部署完成: $TargetPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "请在 RimWorld Mod 管理器中启用 Mod" -ForegroundColor Yellow
} else {
    Write-Host "? 编译失败" -ForegroundColor Red
}
