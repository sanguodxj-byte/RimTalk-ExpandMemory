# Quick Fix Script for MainTabWindow_Memory.cs
# 用于批量替换硬编码英文为翻译键

$filePath = "Source\Memory\UI\MainTabWindow_Memory.cs"

if (!(Test-Path $filePath)) {
    Write-Host "? 文件不存在: $filePath" -ForegroundColor Red
    exit 1
}

Write-Host "?? 开始修复 $filePath..." -ForegroundColor Cyan

# 备份原文件
$backupPath = "$filePath.backup"
Copy-Item $filePath $backupPath -Force
Write-Host "? 已创建备份: $backupPath" -ForegroundColor Green

# 读取文件内容
$content = Get-Content $filePath -Raw -Encoding UTF8

# 应用替换规则（按优先级顺序）
$replacements = @(
    # Tooltip 修复
    @{
        Pattern = 'TooltipHandler\.TipRegion\(pinButtonRect, memory\.isPinned \? "Unpin" : "Pin"\)'
        Replacement = 'TooltipHandler.TipRegion(pinButtonRect, memory.isPinned ? "RimTalk_MindStream_Unpin".Translate() : "RimTalk_MindStream_Pin".Translate())'
    },
    @{
        Pattern = 'TooltipHandler\.TipRegion\(editButtonRect, "Edit"\)'
        Replacement = 'TooltipHandler.TipRegion(editButtonRect, "RimTalk_MindStream_Edit".Translate())'
    },
    @{
        Pattern = 'TooltipHandler\.TipRegion\(importanceBarRect, \$"Importance: \{memory\.importance:F2\}"\)'
        Replacement = 'TooltipHandler.TipRegion(importanceBarRect, "RimTalk_MindStream_ImportanceLabel".Translate(memory.importance.ToString("F2")))'
    },
    @{
        Pattern = 'TooltipHandler\.TipRegion\(activityBarRect, \$"Activity: \{memory\.activity:F2\}"\)'
        Replacement = 'TooltipHandler.TipRegion(activityBarRect, "RimTalk_MindStream_ActivityLabel".Translate(memory.activity.ToString("F2")))'
    },
    
    # Header "with" 修复
    @{
        Pattern = 'header \+= \$" ? with \{memory\.relatedPawnName\}";'
        Replacement = 'header += $" ? {"RimTalk_MindStream_With".Translate()} {memory.relatedPawnName}";'
    },
    
    # Messages.Message 修复
    @{
        Pattern = 'Messages\.Message\("No SCM memories selected", MessageTypeDefOf\.RejectInput, false\)'
        Replacement = 'Messages.Message("RimTalk_MindStream_NoSCMSelected".Translate(), MessageTypeDefOf.RejectInput, false)'
    },
    @{
        Pattern = 'Messages\.Message\("No ELS memories selected", MessageTypeDefOf\.RejectInput, false\)'
        Replacement = 'Messages.Message("RimTalk_MindStream_NoELSSelected".Translate(), MessageTypeDefOf.RejectInput, false)'
    },
    @{
        Pattern = 'Messages\.Message\("No colonists need summarization", MessageTypeDefOf\.RejectInput, false\)'
        Replacement = 'Messages.Message("RimTalk_MindStream_NoNeedSummarization".Translate(), MessageTypeDefOf.RejectInput, false)'
    },
    @{
        Pattern = 'Messages\.Message\("Must enter game first", MessageTypeDefOf\.RejectInput, false\)'
        Replacement = 'Messages.Message("RimTalk_MindStream_MustEnterGame".Translate(), MessageTypeDefOf.RejectInput, false)'
    },
    @{
        Pattern = 'Messages\.Message\("Cannot find memory manager", MessageTypeDefOf\.RejectInput, false\)'
        Replacement = 'Messages.Message("RimTalk_MindStream_CannotFindManager".Translate(), MessageTypeDefOf.RejectInput, false)'
    },
    @{
        Pattern = 'Messages\.Message\(\$"Summarized \{scmMemories\.Count\} memories", MessageTypeDefOf\.PositiveEvent, false\)'
        Replacement = 'Messages.Message("RimTalk_MindStream_SummarizedN".Translate(scmMemories.Count), MessageTypeDefOf.PositiveEvent, false)'
    },
    @{
        Pattern = 'Messages\.Message\(\$"Archived \{elsMemories\.Count\} memories", MessageTypeDefOf\.PositiveEvent, false\)'
        Replacement = 'Messages.Message("RimTalk_MindStream_ArchivedN".Translate(elsMemories.Count), MessageTypeDefOf.PositiveEvent, false)'
    },
    @{
        Pattern = 'Messages\.Message\(\$"Deleted \{count\} memories", MessageTypeDefOf\.PositiveEvent, false\)'
        Replacement = 'Messages.Message("RimTalk_MindStream_DeletedN".Translate(count), MessageTypeDefOf.PositiveEvent, false)'
    },
    @{
        Pattern = 'Messages\.Message\(\$"Queued \{pawnsToSummarize\.Count\} colonists for summarization", MessageTypeDefOf\.TaskCompletion, false\)'
        Replacement = 'Messages.Message("RimTalk_MindStream_QueuedSummarization".Translate(pawnsToSummarize.Count), MessageTypeDefOf.TaskCompletion, false)'
    },
    @{
        Pattern = 'Messages\.Message\(\$"Archived memories for \{count\} colonists", MessageTypeDefOf\.PositiveEvent, false\)'
        Replacement = 'Messages.Message("RimTalk_MindStream_ArchivedForN".Translate(count), MessageTypeDefOf.PositiveEvent, false)'
    },
    
    # Dialog 确认框修复
    @{
        Pattern = '\$"Summarize \{scmMemories\.Count\} SCM memories to ELS\?"'
        Replacement = '"RimTalk_MindStream_SummarizeConfirm".Translate(scmMemories.Count)'
    },
    @{
        Pattern = '\$"Archive \{elsMemories\.Count\} ELS memories to CLPA\?"'
        Replacement = '"RimTalk_MindStream_ArchiveConfirm".Translate(elsMemories.Count)'
    },
    @{
        Pattern = '\$"Delete \{count\} selected memories\?"'
        Replacement = '"RimTalk_MindStream_DeleteConfirm".Translate(count)'
    },
    
    # Widgets.Label 修复
    @{
        Pattern = 'Widgets\.Label\(rect, "Select a colonist to view memories"\)'
        Replacement = 'Widgets.Label(rect, "RimTalk_MindStream_SelectColonist".Translate())'
    },
    @{
        Pattern = 'Widgets\.Label\(rect, "Selected pawn has no memory component"\)'
        Replacement = 'Widgets.Label(rect, "RimTalk_MindStream_NoMemoryComp".Translate())'
    }
)

$fixedCount = 0

foreach ($replacement in $replacements) {
    $pattern = $replacement.Pattern
    $newValue = $replacement.Replacement
    
    if ($content -match $pattern) {
        $content = $content -replace $pattern, $newValue
        $fixedCount++
        Write-Host "? 修复: $($pattern.Substring(0, [Math]::Min(50, $pattern.Length)))..." -ForegroundColor Green
    }
}

# 保存修改后的文件
$content | Set-Content $filePath -Encoding UTF8 -NoNewline

Write-Host "`n? 完成! 共修复 $fixedCount 处硬编码文本" -ForegroundColor Cyan
Write-Host "?? 备份文件: $backupPath" -ForegroundColor Yellow
Write-Host "`n?? 请手动检查修复结果，确保没有遗漏或错误" -ForegroundColor Yellow

# 显示仍需手动修复的内容
Write-Host "`n?? 检查是否还有未修复的硬编码英文..." -ForegroundColor Cyan

$patterns = @(
    'Messages\.Message\("(?!RimTalk_)',
    'Widgets\.Label\(.*?, "(?!RimTalk_)',
    'TooltipHandler\.TipRegion\(.*?, "(?!RimTalk_)',
    'Dialog_MessageBox\.CreateConfirmation\(\s*"(?!RimTalk_)'
)

$foundIssues = $false
foreach ($pattern in $patterns) {
    $matches = [regex]::Matches($content, $pattern)
    if ($matches.Count -gt 0) {
        Write-Host "?? 发现 $($matches.Count) 处可能的硬编码: $pattern" -ForegroundColor Yellow
        $foundIssues = $true
    }
}

if (!$foundIssues) {
    Write-Host "? 未发现明显的硬编码英文!" -ForegroundColor Green
}

Write-Host "`n?? 下一步:" -ForegroundColor Cyan
Write-Host "1. 用 Visual Studio/VSCode 打开文件检查" -ForegroundColor White
Write-Host "2. 编译测试: dotnet build" -ForegroundColor White
Write-Host "3. 启动游戏测试UI" -ForegroundColor White
Write-Host "4. 切换语言测试翻译" -ForegroundColor White

# 询问是否编译
Write-Host "`n是否立即编译测试? (Y/N): " -NoNewline -ForegroundColor Cyan
$response = Read-Host

if ($response -eq 'Y' -or $response -eq 'y') {
    Write-Host "`n?? 开始编译..." -ForegroundColor Cyan
    dotnet build
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "`n? 编译成功!" -ForegroundColor Green
    } else {
        Write-Host "`n? 编译失败! 请检查错误信息" -ForegroundColor Red
        Write-Host "?? 提示: 如果有语法错误，请还原备份文件并手动修复" -ForegroundColor Yellow
    }
}
