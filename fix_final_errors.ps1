# 修复最后的类型引用错误

Write-Host "Fixing final type reference errors..." -ForegroundColor Cyan

# 需要添加 using RimTalk.Memory; 的文件
$files = @(
    'Source/Memory/UI/Dialog_InjectionPreview.cs',
    'Source/Memory/UI/MainTabWindow_Memory.cs',
    'Source/Memory/Debug/DialogInjectionPreview_VectorPanelPatch.cs'
)

foreach ($file in $files) {
    if (Test-Path $file) {
        Write-Host "`nProcessing: $file" -ForegroundColor Cyan
        $content = Get-Content $file -Raw -Encoding UTF8
        
        # 检查是否已有 using RimTalk.Memory;
        if ($content -notmatch 'using RimTalk\.Memory;') {
            # 查找第一个 using 语句的位置，在其后添加
            if ($content -match '(using\s+\w+[^;]*;)') {
                $content = $content -replace '(using\s+System[^;]*;)', "`$1`r`nusing RimTalk.Memory;"
                Write-Host "  Added using RimTalk.Memory;" -ForegroundColor Green
            }
        } else {
            Write-Host "  Already has using RimTalk.Memory;" -ForegroundColor Yellow
        }
        
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText((Resolve-Path $file).Path, $content, $utf8NoBom)
    }
}

Write-Host "`nDone! All type reference errors should be fixed." -ForegroundColor Green
Write-Host "Run 'dotnet build RimTalk-ExpandMemory.csproj -c Release' to verify." -ForegroundColor Yellow
