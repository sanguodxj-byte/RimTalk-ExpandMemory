# 修复剩余的编译错误

Write-Host "Fixing remaining compilation errors..." -ForegroundColor Cyan

# 1. 修复 RimTalkSettings.cs 中的 Debug.Dialog_InjectionPreview 引用
$file = 'Source/RimTalkSettings.cs'
if (Test-Path $file) {
    Write-Host "`nProcessing: $file" -ForegroundColor Cyan
    $content = Get-Content $file -Raw -Encoding UTF8
    
    # 替换 new Debug.Dialog_InjectionPreview() 为 new Memory.Debug.Dialog_InjectionPreview()
    $content = $content -replace 'new Debug\.Dialog_InjectionPreview\(\)', 'new Memory.Debug.Dialog_InjectionPreview()'
    
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText((Resolve-Path $file).Path, $content, $utf8NoBom)
    Write-Host "  Fixed Debug.Dialog_InjectionPreview reference" -ForegroundColor Green
}

# 2. 修复 IncidentPatch.cs 中的 RimTalk.Memory 引用
$file = 'Source/Patches/IncidentPatch.cs'
if (Test-Path $file) {
    Write-Host "`nProcessing: $file" -ForegroundColor Cyan
    $content = Get-Content $file -Raw -Encoding UTF8
    
    # 检查是否已有 using RimTalk.Memory;
    if ($content -notmatch 'using RimTalk\.Memory;') {
        # 在 using RimTalk.MemoryPatch; 之后添加
        $content = $content -replace '(using RimTalk\.MemoryPatch;)', "`$1`r`nusing RimTalk.Memory;"
        Write-Host "  Added using RimTalk.Memory;" -ForegroundColor Green
    }
    
    # 替换 RimTalk.Memory. 为直接引用
    $content = $content -replace 'RimTalk\.Memory\.', ''
    
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText((Resolve-Path $file).Path, $content, $utf8NoBom)
    Write-Host "  Fixed RimTalk.Memory references" -ForegroundColor Green
}

# 3. 修复 MainTabWindow_Memory.cs
$file = 'Source/Memory/UI/MainTabWindow_Memory.cs'
if (Test-Path $file) {
    Write-Host "`nProcessing: $file" -ForegroundColor Cyan
    $content = Get-Content $file -Raw -Encoding UTF8
    
    # 检查是否已有 using RimTalk.MemoryPatch;
    if ($content -notmatch 'using RimTalk\.MemoryPatch;') {
        $content = $content -replace '(namespace\s+)', "using RimTalk.MemoryPatch;`r`n`r`n`$1"
        Write-Host "  Added using RimTalk.MemoryPatch;" -ForegroundColor Green
    }
    
    # 替换 RimTalk.Memory. 为直接引用
    $content = $content -replace 'RimTalk\.Memory\.', ''
    
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText((Resolve-Path $file).Path, $content, $utf8NoBom)
    Write-Host "  Fixed references" -ForegroundColor Green
}

# 4. 修复 Dialog_InjectionPreview.cs
$file = 'Source/Memory/UI/Dialog_InjectionPreview.cs'
if (Test-Path $file) {
    Write-Host "`nProcessing: $file" -ForegroundColor Cyan
    $content = Get-Content $file -Raw -Encoding UTF8
    
    # 检查是否已有 using RimTalk.MemoryPatch;
    if ($content -notmatch 'using RimTalk\.MemoryPatch;') {
        $content = $content -replace '(namespace\s+)', "using RimTalk.MemoryPatch;`r`n`r`n`$1"
        Write-Host "  Added using RimTalk.MemoryPatch;" -ForegroundColor Green
    }
    
    # 替换 RimTalk.Memory. 为直接引用
    $content = $content -replace 'RimTalk\.Memory\.', ''
    
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText((Resolve-Path $file).Path, $content, $utf8NoBom)
    Write-Host "  Fixed references" -ForegroundColor Green
}

Write-Host "`nDone! All remaining errors should be fixed." -ForegroundColor Green
Write-Host "Run 'dotnet build RimTalk-ExpandMemory.csproj -c Release' to verify." -ForegroundColor Yellow
