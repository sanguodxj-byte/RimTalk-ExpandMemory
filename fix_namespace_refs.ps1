# 修复命名空间引用问题
# 将 RimTalk.MemoryPatch.RimTalkMemoryPatchMod 替换为 RimTalkMemoryPatchMod

$files = @(
    'Source/Memory/CommonKnowledgeLibrary.cs',
    'Source/Memory/DynamicMemoryInjection.cs',
    'Source/Memory/AdaptiveThresholdManager.cs',
    'Source/Memory/AI/IndependentAISummarizer.cs',
    'Source/Memory/ProactiveMemoryRecall.cs',
    'Source/Patches/IncidentPatch.cs',
    'Source/Memory/UI/MainTabWindow_Memory.cs',
    'Source/Memory/UI/Dialog_InjectionPreview.cs'
)

$fixedCount = 0
$errorCount = 0

foreach ($file in $files) {
    if (Test-Path $file) {
        try {
            Write-Host "Processing: $file" -ForegroundColor Cyan
            
            # 读取文件内容
            $content = Get-Content $file -Raw -Encoding UTF8
            
            # 执行替换
            $newContent = $content -replace 'RimTalk\.MemoryPatch\.RimTalkMemoryPatchMod', 'RimTalkMemoryPatchMod'
            
            # 检查是否有变化
            if ($content -ne $newContent) {
                # 写回文件
                $utf8NoBom = New-Object System.Text.UTF8Encoding $false
                [System.IO.File]::WriteAllText((Resolve-Path $file).Path, $newContent, $utf8NoBom)
                
                Write-Host "  Fixed: $file" -ForegroundColor Green
                $fixedCount++
            } else {
                Write-Host "  No changes needed: $file" -ForegroundColor Yellow
            }
        }
        catch {
            Write-Host "  Error processing $file : $_" -ForegroundColor Red
            $errorCount++
        }
    } else {
        Write-Host "  File not found: $file" -ForegroundColor Red
        $errorCount++
    }
}

Write-Host "`nSummary:" -ForegroundColor Cyan
Write-Host "  Fixed: $fixedCount files" -ForegroundColor Green
Write-Host "  Errors: $errorCount files" -ForegroundColor Red

# 同时需要添加 using 语句到需要的文件
Write-Host "`nAdding using statements..." -ForegroundColor Cyan

$filesToAddUsing = @(
    'Source/Memory/DynamicMemoryInjection.cs',
    'Source/Memory/AdaptiveThresholdManager.cs',
    'Source/Memory/AI/IndependentAISummarizer.cs',
    'Source/Memory/ProactiveMemoryRecall.cs',
    'Source/Patches/IncidentPatch.cs',
    'Source/Memory/UI/MainTabWindow_Memory.cs',
    'Source/Memory/UI/Dialog_InjectionPreview.cs'
)

foreach ($file in $filesToAddUsing) {
    if (Test-Path $file) {
        try {
            $content = Get-Content $file -Raw -Encoding UTF8
            
            # 检查是否已经有 using RimTalk.MemoryPatch;
            if ($content -notmatch 'using RimTalk\.MemoryPatch;') {
                # 在第一个 namespace 之前添加 using
                $newContent = $content -replace '(namespace\s+)', "using RimTalk.MemoryPatch;`r`n`r`n`$1"
                
                $utf8NoBom = New-Object System.Text.UTF8Encoding $false
                [System.IO.File]::WriteAllText((Resolve-Path $file).Path, $newContent, $utf8NoBom)
                
                Write-Host "  Added using to: $file" -ForegroundColor Green
            }
        }
        catch {
            Write-Host "  Error adding using to $file : $_" -ForegroundColor Red
        }
    }
}

Write-Host "`nDone!" -ForegroundColor Green
