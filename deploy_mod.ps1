Param(
    [string]$ModPath = "D:\steam\steamapps\common\RimWorld\Mods\RimTalk-ExpandMemory",
    [switch]$SkipBuild
)

# ========== 配置 ==========
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $repoRoot "RimTalk-ExpandMemory.csproj"
$buildConfig = "Release"
$buildDir = Join-Path $repoRoot "bin\$buildConfig"
$assemblyName = "RimTalkMemoryPatch.dll"  # ? 修正：使用实际的DLL名称
$targetAssemblyDir = Join-Path $ModPath "1.6\Assemblies"

# ========== 步骤1：可选编译 ==========
if (-not $SkipBuild) {
    Write-Host "[1/4] 编译项目 ($buildConfig)..." -ForegroundColor Yellow
    dotnet build $project -c $buildConfig | Out-Null
}

# ========== 步骤2：检查输出 ==========
$srcDll = Join-Path $buildDir $assemblyName
if (-not (Test-Path $srcDll)) {
    throw "未找到编译输出: $srcDll"
}

# ========== 步骤3：拷贝 DLL ==========
Write-Host "[2/4] 拷贝 DLL -> $targetAssemblyDir" -ForegroundColor Yellow
if (-not (Test-Path $targetAssemblyDir)) { New-Item -ItemType Directory -Path $targetAssemblyDir -Force | Out-Null }
Copy-Item $srcDll $targetAssemblyDir -Force

# ========== 步骤4：可选拷贝 PDB（便于调试） ==========
$pdb = [System.IO.Path]::ChangeExtension($srcDll, ".pdb")
if (Test-Path $pdb) {
    Copy-Item $pdb $targetAssemblyDir -Force
}

# ========== 完成 ==========
Write-Host "[3/4] 部署完成: $assemblyName" -ForegroundColor Green
Write-Host "[4/4] 目标目录: $targetAssemblyDir" -ForegroundColor Green
Write-Host "提示: 需要重新启动 RimWorld 以加载新 DLL" -ForegroundColor Cyan
