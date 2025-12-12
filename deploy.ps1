# RimWorld Mod 快速部署脚本
# 用法：.\deploy.ps1

param(
    [string]$RimWorldDir = "D:\steam\steamapps\common\RimWorld",
    [string]$ModName = "RimTalk-MemoryPatch",
    [string]$GameVersion = "1.6"
)

$ErrorActionPreference = "Stop"

# 颜色输出函数
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RimWorld Mod 部署工具" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查 RimWorld 目录
if (-not (Test-Path $RimWorldDir)) {
    Write-Host "? 错误：找不到 RimWorld 目录" -ForegroundColor Red
    Write-Host "路径：$RimWorldDir" -ForegroundColor Yellow
    exit 1
}

Write-Host "? RimWorld 目录：$RimWorldDir" -ForegroundColor Green

# 目标路径
$modPath = "$RimWorldDir\Mods\$ModName"
Write-Host "? Mod 目标路径：$modPath" -ForegroundColor Green
Write-Host ""

# 创建 Mod 目录
if (-not (Test-Path $modPath)) {
    New-Item -ItemType Directory -Path $modPath -Force | Out-Null
    Write-Host "? 已创建 Mod 目录" -ForegroundColor Green
}

# 复制文件夹
$folders = @("About", "Defs", "Languages", "Textures", $GameVersion)

foreach ($folder in $folders) {
    $source = Join-Path $PSScriptRoot $folder
    $dest = Join-Path $modPath $folder
    
    if (Test-Path $source) {
        Write-Host "正在复制 $folder..." -NoNewline
        
        # 使用 robocopy 镜像同步
        $result = robocopy $source $dest /MIR /NJH /NJS /NDL /NFL /NC /NS
        
        if ($LASTEXITCODE -le 7) {
            Write-Host " ?" -ForegroundColor Green
        } else {
            Write-Host " ?" -ForegroundColor Yellow
        }
    } else {
        Write-Host "? 跳过 $folder（不存在）" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  部署完成！" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Mod 已部署到：" -ForegroundColor Cyan
Write-Host $modPath -ForegroundColor Yellow
Write-Host ""
Write-Host "下一步：启动 RimWorld 并在 Mod 列表中启用 '$ModName'" -ForegroundColor Cyan
Write-Host ""
