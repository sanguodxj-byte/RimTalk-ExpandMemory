using System;
using System.Text;
using RimTalk.Memory;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;
using RimTalk.Memory.Patches;
using RimTalk.Memory.VectorDB;

namespace RimTalk.Memory.Debug
{
    /// <summary>
    /// 调试预览器 - 模拟RimTalk预期发送的JSON，分析记忆和常识注入内容
    /// </summary>
    public class Dialog_InjectionPreview : Window
    {
        private Pawn selectedPawn;
        private Pawn targetPawn;  // ⭐ 新增：目标Pawn
        private Vector2 scrollPosition;
        private string cachedPreview = "";
        private int cachedMemoryCount = 0;
        private int cachedKnowledgeCount = 0;
        private string contextInput = "";  // ⭐ 新增：上下文输入
        private string lastRefreshedContext = "";  // ⭐ 新增：上次刷新时的上下文
        
        // ⭐ 新增：注入预览增强
        private bool showRejectedKnowledge = false; // 是否显示未注入的常识
        private List<KnowledgeScoreDetail> cachedAllKnowledgeScores = null; // 缓存所有评分

        public override Vector2 InitialSize => new Vector2(1100f, 800f);

        public Dialog_InjectionPreview()
        {
            this.doCloseX = true;
            this.doCloseButton = true;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
            
            // 默认选择第一个殖民者
            if (Find.CurrentMap != null)
            {
                selectedPawn = Find.CurrentMap.mapPawns.FreeColonists.FirstOrDefault();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            float yPos = 0f;

            // 标题
            Text.Font = GameFont.Medium;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            Widgets.Label(new Rect(0f, yPos, 500f, 35f), "调试预览器 - RimTalk JSON 模拟");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            yPos += 40f;

            // 殖民者选择器（当前角色 + 目标角色）
            DrawPawnSelectors(new Rect(0f, yPos, inRect.width, 80f));
            yPos += 85f;

            if (selectedPawn == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, inRect.height / 2 - 20f, inRect.width, 40f), 
                    "没有可用的殖民者\n\n请进入游戏并加载存档");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // ⭐ 新增：上下文输入框
            DrawContextInput(new Rect(0f, yPos, inRect.width, 80f));
            yPos += 85f;

            // 统计信息
            DrawStats(new Rect(0f, yPos, inRect.width, 80f));
            yPos += 85f;

            // 两个按钮：显示/隐藏未注入 + 刷新预览
            Rect toggleButtonRect = new Rect(inRect.width - 230f, yPos, 110f, 35f);
            string toggleLabel = showRejectedKnowledge ? "隐藏未注入" : "显示未注入";
            if (Widgets.ButtonText(toggleButtonRect, toggleLabel))
            {
                showRejectedKnowledge = !showRejectedKnowledge;
                RefreshPreview(); // 刷新显示
            }
            
            Rect refreshButtonRect = new Rect(inRect.width - 110f, yPos, 100f, 35f);
            if (Widgets.ButtonText(refreshButtonRect, "刷新预览"))
            {
                RefreshPreview();
            }
            yPos += 40f;

            // 预览区域
            Rect previewRect = new Rect(0f, yPos, inRect.width, inRect.height - yPos - 50f);
            DrawPreview(previewRect);
        }
        
        private void DrawPawnSelectors(Rect rect)
        {
            // 第一行：当前角色选择器
            GUI.color = new Color(0.8f, 0.9f, 1f);
            Widgets.Label(new Rect(rect.x, rect.y, 120f, rect.height / 2), "当前角色：");
            GUI.color = Color.white;

            Rect buttonRect = new Rect(rect.x + 130f, rect.y, 200f, 35f);
            
            string label = selectedPawn != null ? selectedPawn.LabelShort : "无";
            if (Widgets.ButtonText(buttonRect, label))
            {
                ShowPawnSelectionMenu(isPrimary: true);
            }

            // 显示选中殖民者的基本信息
            if (selectedPawn != null)
            {
                GUI.color = Color.gray;
                string info = $"{selectedPawn.def.label}";
                info += $" | {selectedPawn.gender.GetLabel()}";
                Widgets.Label(new Rect(rect.x + 340f, rect.y + 8f, 300f, rect.height / 2), info);
                GUI.color = Color.white;
            }

            // 第二行：目标角色选择器 ⭐ 新增
            float secondRowY = rect.y + 40f;
            GUI.color = new Color(1f, 0.9f, 0.8f);
            Widgets.Label(new Rect(rect.x, secondRowY, 120f, rect.height / 2), "目标角色：");
            GUI.color = Color.white;

            Rect targetButtonRect = new Rect(rect.x + 130f, secondRowY, 200f, 35f);
            
            string targetLabel = targetPawn != null ? targetPawn.LabelShort : "无（点击选择）";
            if (Widgets.ButtonText(targetButtonRect, targetLabel))
            {
                ShowPawnSelectionMenu(isPrimary: false);
            }

            // 显示目标角色信息
            if (targetPawn != null)
            {
                GUI.color = Color.gray;
                string targetInfo = $"{targetPawn.def.label}";
                targetInfo += $" | {targetPawn.gender.GetLabel()}";
                Widgets.Label(new Rect(rect.x + 340f, secondRowY + 8f, 300f, rect.height / 2), targetInfo);
                GUI.color = Color.white;
                
                // 清除按钮
                Rect clearButtonRect = new Rect(rect.x + 650f, secondRowY, 80f, 35f);
                if (Widgets.ButtonText(clearButtonRect, "清除"))
                {
                    targetPawn = null;
                    cachedPreview = ""; // 清空缓存
                }
            }
        }

        /// <summary>
        /// 显示Pawn选择菜单（支持主要角色和目标角色）
        /// ⭐ 修改：支持所有类人生物，不仅限于殖民者
        /// </summary>
        private void ShowPawnSelectionMenu(bool isPrimary)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            if (Find.CurrentMap != null)
            {
                // ⭐ 获取所有类人生物，而不只是殖民者
                var allHumanlikes = Find.CurrentMap.mapPawns.AllPawnsSpawned
                    .Where(p => p.RaceProps.Humanlike)
                    .OrderBy(p =>
                    {
                        // 排序优先级：1=殖民者，2=囚犯，3=奴隶，4=访客，5=其他
                        if (p.IsColonist) return 1;
                        if (p.IsPrisoner) return 2;
                        if (p.IsSlaveOfColony) return 3;
                        if (p.HostFaction == Faction.OfPlayer) return 4;
                        return 5;
                    })
                    .ThenBy(p => p.LabelShort);
                
                foreach (var pawn in allHumanlikes)
                {
                    Pawn localPawn = pawn;
                    
                    // 构建选项标签，显示身份
                    string optionLabel = pawn.LabelShort;
                    
                    // 添加身份标识
                    if (pawn.IsColonist)
                    {
                        optionLabel += " (殖民者)";
                    }
                    else if (pawn.IsPrisoner)
                    {
                        optionLabel += " (囚犯)";
                    }
                    else if (pawn.IsSlaveOfColony)
                    {
                        optionLabel += " (奴隶)";
                    }
                    else if (pawn.HostFaction == Faction.OfPlayer)
                    {
                        optionLabel += " (访客)";
                    }
                    else if (pawn.Faction != null && pawn.Faction != Faction.OfPlayer)
                    {
                        optionLabel += $" ({pawn.Faction.Name})";
                    }
                    
                    // 如果是选择目标角色，且与当前角色相同，添加提示
                    if (!isPrimary && selectedPawn != null && pawn == selectedPawn)
                    {
                        optionLabel += " (与当前角色相同)";
                    }
                    
                    options.Add(new FloatMenuOption(optionLabel, delegate
                    {
                        if (isPrimary)
                        {
                            selectedPawn = localPawn;
                            // 如果新选的当前角色与目标角色相同，清除目标角色
                            if (targetPawn == localPawn)
                            {
                                targetPawn = null;
                            }
                        }
                        else
                        {
                            targetPawn = localPawn;
                        }
                        cachedPreview = ""; // 清空缓存，强制刷新
                    }));
                }
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
            else
            {
                Messages.Message("当前地图上没有可用的类人生物", MessageTypeDefOf.RejectInput, false);
            }
        }

        private void DrawStats(Rect rect)
        {
            if (selectedPawn == null) return;

            var memoryComp = selectedPawn.TryGetComp<FourLayerMemoryComp>();
            if (memoryComp == null)
            {
                GUI.color = Color.yellow;
                Widgets.Label(rect, "该殖民者没有记忆组件");
                GUI.color = Color.white;
                return;
            }

            // 背景框
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            Rect innerRect = rect.ContractedBy(5f);

            float x = innerRect.x;
            float lineHeight = Text.LineHeight;

            // 第一行 - 记忆统计
            GUI.color = new Color(0.8f, 1f, 0.8f);
            Widgets.Label(new Rect(x, innerRect.y, 150f, lineHeight), "记忆层级统计：");
            GUI.color = Color.white;

            x += 120f;
            GUI.color = new Color(0.7f, 0.7f, 1f);
            Widgets.Label(new Rect(x, innerRect.y, 200f, lineHeight), 
                $"ABM: {memoryComp.ActiveMemories.Count}/6 (固定，不注入)");
            GUI.color = Color.white;
            
            x += 220f;
            Widgets.Label(new Rect(x, innerRect.y, 150f, lineHeight), 
                $"SCM: {memoryComp.SituationalMemories.Count}");
            
            x += 120f;
            Widgets.Label(new Rect(x, innerRect.y, 150f, lineHeight), 
                $"ELS: {memoryComp.EventLogMemories.Count}");
            
            x += 120f;
            Widgets.Label(new Rect(x, innerRect.y, 150f, lineHeight), 
                $"CLPA: {memoryComp.ArchiveMemories.Count}");

            // 第二行 - 常识统计
            x = innerRect.x;
            GUI.color = new Color(1f, 1f, 0.8f);
            Widgets.Label(new Rect(x, innerRect.y + lineHeight + 5f, 150f, lineHeight), "常识库统计：");
            GUI.color = Color.white;

            x += 120f;
            var library = MemoryManager.GetCommonKnowledge();
            int totalKnowledge = library.Entries.Count;
            int enabledKnowledge = library.Entries.Count(e => e.isEnabled);
            
            Widgets.Label(new Rect(x, innerRect.y + lineHeight + 5f, 300f, lineHeight), 
                $"总数: {totalKnowledge} | 启用: {enabledKnowledge}");

            // 第三行 - 注入配置
            x = innerRect.x;
            GUI.color = new Color(0.8f, 0.8f, 1f);
            Widgets.Label(new Rect(x, innerRect.y + lineHeight * 2 + 10f, 150f, lineHeight), "注入配置：");
            GUI.color = Color.white;

            x += 120f;
            var settings = RimTalkMemoryPatchMod.Settings;
            if (settings != null)
            {
                string mode = settings.useDynamicInjection ? "动态评分" : "静态顺序";
                Widgets.Label(new Rect(x, innerRect.y + lineHeight * 2 + 10f, 700f, lineHeight), 
                    $"模式: {mode} | 最大记忆: {settings.maxInjectedMemories} | 最大常识: {settings.maxInjectedKnowledge} | " +
                    $"记忆阈值: {settings.memoryScoreThreshold:F2} | 常识阈值: {settings.knowledgeScoreThreshold:F2}");
            }
        }

        private void DrawPreview(Rect rect)
        {
            // 如果缓存为空，自动刷新
            if (string.IsNullOrEmpty(cachedPreview))
            {
                RefreshPreview();
            }

            // 背景
            Widgets.DrawBoxSolid(rect, new Color(0.1f, 0.1f, 0.1f, 0.8f));

            // 滚动视图
            Rect innerRect = rect.ContractedBy(10f);
            float contentHeight = Text.CalcHeight(cachedPreview, innerRect.width - 20f);
            Rect viewRect = new Rect(0f, 0f, innerRect.width - 20f, contentHeight + 50f);

            Widgets.BeginScrollView(innerRect, ref scrollPosition, viewRect);

            // 显示内容
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Widgets.Label(new Rect(0f, 0f, viewRect.width, contentHeight), cachedPreview);
            GUI.color = Color.white;

            Widgets.EndScrollView();
        }

        private void RefreshPreview()
        {
            if (selectedPawn == null)
            {
                cachedPreview = "未选择殖民者";
                return;
            }

            var memoryComp = selectedPawn.TryGetComp<FourLayerMemoryComp>();
            if (memoryComp == null)
            {
                cachedPreview = "该殖民者没有记忆组件";
                return;
            }

            var settings = RimTalkMemoryPatchMod.Settings;
            if (settings == null)
            {
                cachedPreview = "无法加载Mod设置";
                return;
            }

            try
            {
                var preview = new System.Text.StringBuilder();
                
                // ===== 模拟 RimTalk JSON 结构 =====
                preview.AppendLine("╔════════════════════════════════════════════════════════════════════════╗");
                preview.AppendLine("║        RimTalk API JSON 请求模拟 (ExpandMemory 注入部分)             ║");
                preview.AppendLine("╚════════════════════════════════════════════════════════════════════════╝");
                preview.AppendLine();
                
                preview.AppendLine($"殖民者: {selectedPawn.LabelShort}");
                if (targetPawn != null)
                {
                    preview.AppendLine($"目标角色: {targetPawn.LabelShort}");
                }
                preview.AppendLine($"时间: {Find.TickManager.TicksGame.ToStringTicksToPeriod()}");
                preview.AppendLine($"注入模式: {(settings.useDynamicInjection ? "动态评分" : "静态顺序")}");
                
                // ⭐ 显示上下文输入状态
                if (string.IsNullOrEmpty(contextInput))
                {
                    preview.AppendLine($"上下文: 空（基于重要性+层级评分）");
                }
                else
                {
                    preview.AppendLine($"上下文: \"{contextInput.Substring(0, Math.Min(50, contextInput.Length))}...\"");
                }
                preview.AppendLine();
                
                // 先获取记忆和常识内容
                string memoryInjection = null;
                string knowledgeInjection = null;
                List<DynamicMemoryInjection.MemoryScore> memoryScores = null;
                List<KnowledgeScore> knowledgeScores = null;

                if (settings.useDynamicInjection)
                {
                    // ⭐ 使用用户输入的上下文
                    string actualContext = string.IsNullOrEmpty(contextInput) ? "" : contextInput;
                    
                    memoryInjection = DynamicMemoryInjection.InjectMemoriesWithDetails(
                        memoryComp, 
                        actualContext,  // ⬅️ 使用实际上下文
                        settings.maxInjectedMemories,
                        out memoryScores
                    );
                }

                var library = MemoryManager.GetCommonKnowledge();
                KeywordExtractionInfo keywordInfo;
                List<KnowledgeScoreDetail> allKnowledgeScores;
                
                // ⭐ 使用实际上下文（如果为空，则使用角色名作为种子）
                string testContext = string.IsNullOrEmpty(contextInput) ? "" : contextInput;
                if (string.IsNullOrEmpty(testContext))
                {
                    testContext = selectedPawn != null ? selectedPawn.LabelShort : "";
                    if (targetPawn != null)
                    {
                        testContext += " " + targetPawn.LabelShort;
                    }
                }
                
                // ⭐ 传递targetPawn参数,并获取所有评分详情
                knowledgeInjection = library.InjectKnowledgeWithDetails(
                    testContext,  // ⬅️ 使用实际上下文
                    settings.maxInjectedKnowledge,
                    out knowledgeScores,
                    out allKnowledgeScores,  // ⭐ 获取所有评分
                    out keywordInfo,
                    selectedPawn,
                    targetPawn
                );

                // ⭐ 新增：手动模拟向量匹配（因为库函数中已移除，移到了 Patch 中）
                if (settings.enableVectorEnhancement && !string.IsNullOrEmpty(testContext))
                {
                    try 
                    {
                        // 使用 ContextCleaner 清理上下文
                        string cleanedContext = ContextCleaner.CleanForVectorMatching(testContext);
                        
                        // 同步调用向量搜索
                        var vectorResults = VectorService.Instance.FindBestLoreIds(
                            cleanedContext,
                            settings.maxVectorResults,
                            settings.vectorSimilarityThreshold
                        );
                        
                        if (vectorResults != null && vectorResults.Count > 0)
                        {
                            var vectorSb = new StringBuilder();
                            
                            // 如果已有标签匹配内容，添加分隔
                            if (!string.IsNullOrEmpty(knowledgeInjection))
                            {
                                vectorSb.AppendLine();
                                vectorSb.AppendLine(); // 空行分隔
                            }
                            
                            vectorSb.AppendLine("## World Knowledge (Vector Enhanced)");
                            
                            foreach (var (id, similarity) in vectorResults)
                            {
                                var entry = library.Entries.FirstOrDefault(e => e.id == id);
                                if (entry != null)
                                {
                                    vectorSb.AppendLine($"[{entry.tag}|{similarity:F2}] {entry.content}");
                                    
                                    // 更新评分详情列表
                                    var existingDetail = allKnowledgeScores.FirstOrDefault(d => d.Entry == entry);
                                    if (existingDetail != null)
                                    {
                                        // 已存在：更新为混合匹配
                                        existingDetail.MatchType = KnowledgeMatchType.Mixed;
                                        float vectorScore = 100f * similarity;
                                        existingDetail.TotalScore += vectorScore; // 叠加分数用于排序展示
                                        existingDetail.MatchTypeScore += vectorScore;
                                        
                                        // 向量匹配总是被选中
                                        if (existingDetail.FailReason != "Selected")
                                        {
                                            existingDetail.FailReason = "Selected (Vector)";
                                        }
                                    }
                                    else
                                    {
                                        // 新增：向量匹配
                                        allKnowledgeScores.Add(new KnowledgeScoreDetail
                                        {
                                            Entry = entry,
                                            IsEnabled = entry.isEnabled,
                                            TotalScore = 100f * similarity + entry.importance,
                                            BaseScore = entry.importance,
                                            MatchTypeScore = 100f * similarity,
                                            MatchType = KnowledgeMatchType.Vector,
                                            FailReason = "Selected (Vector)",
                                            MatchedTags = entry.GetTags()
                                        });
                                    }
                                }
                            }
                            
                            // 追加到 knowledgeInjection
                            knowledgeInjection += vectorSb.ToString();
                            
                            // 重新排序详情列表
                            allKnowledgeScores.Sort((a, b) => b.TotalScore.CompareTo(a.TotalScore));
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[RimTalk-ExpandMemory] Preview vector search failed: {ex.Message}");
                    }
                }
                
                // ⭐ 缓存所有评分详情
                cachedAllKnowledgeScores = allKnowledgeScores;

                cachedMemoryCount = memoryScores?.Count ?? 0;
                // 更新常识计数：统计所有 FailReason 为 Selected 或 Selected (Vector) 的条目
                cachedKnowledgeCount = allKnowledgeScores.Count(s => s.FailReason.StartsWith("Selected"));
                
                // 构建完整的system content
                var systemContent = new System.Text.StringBuilder();
                
                // 【优先级1: 常识库】- 放在最上方，可以覆盖RimTalk内置提示词
                if (!string.IsNullOrEmpty(knowledgeInjection))
                {
                    systemContent.AppendLine("【常识】");
                    systemContent.AppendLine(knowledgeInjection);
                    systemContent.AppendLine();
                }
                
                // 【优先级2: RimTalk内置提示词将在这里】
                systemContent.AppendLine("你是一个RimWorld殖民地的角色扮演AI。");
                systemContent.AppendLine($"你正在扮演 {selectedPawn.LabelShort}。");
                systemContent.AppendLine();
                
                // 【优先级3: 记忆】- 放在最后，提供上下文
                if (!string.IsNullOrEmpty(memoryInjection))
                {
                    systemContent.AppendLine("【记忆】");
                    systemContent.AppendLine(memoryInjection);
                    systemContent.AppendLine();
                }

                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine("📋 完整的 JSON 请求结构:");
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine();
                
                preview.AppendLine("{");
                preview.AppendLine("  \"model\": \"gpt-4\",");
                preview.AppendLine("  \"messages\": [");
                preview.AppendLine("    {");
                preview.AppendLine("      \"role\": \"system\",");
                preview.AppendLine("      \"content\": \"");
                
                // 显示实际的system content，带缩进和转义
                var systemLines = systemContent.ToString().Split('\n');
                foreach (var line in systemLines.Take(20)) // 限制显示前20行
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        string escapedLine = line.Replace("\"", "\\\"").Replace("\r", "");
                        preview.AppendLine($"        {escapedLine}");
                    }
                }
                
                if (systemLines.Length > 20)
                {
                    preview.AppendLine($"        ... (共 {systemLines.Length} 行，省略剩余部分)");
                }
                
                preview.AppendLine("      \"");
                preview.AppendLine("    }, ");
                preview.AppendLine("    {");
                preview.AppendLine("      \"role\": \"user\", ");
                preview.AppendLine("      \"content\": \"[用户输入的对话内容]\"");
                preview.AppendLine("    }");
                preview.AppendLine("  ],");
                preview.AppendLine("  \"temperature\": 0.7,");
                preview.AppendLine("  \"max_tokens\": 500");
                preview.AppendLine("}");
                preview.AppendLine();
                
                // ===== 记忆注入详细分析 =====
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine("📝 【ExpandMemory - 记忆注入详细分析】");
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine();

                if (memoryInjection != null && memoryScores != null)
                {
                    preview.AppendLine($"🎯 动态评分选择了 {memoryScores.Count} 条记忆");
                    preview.AppendLine($"📊 评分阈值: {settings.memoryScoreThreshold:F2} (低于此分数不注入)");
                    preview.AppendLine();

                    // 显示评分详情
                    for (int i = 0; i < memoryScores.Count; i++)
                    {
                        var score = memoryScores[i];
                        var memory = score.Memory;
                        
                        // 使用颜色代码标注来源
                        string source = GetMemorySourceTag(memory.layer);
                        string colorTag = GetMemoryColorTag(memory.layer);
                        
                        preview.AppendLine($"[{i + 1}] {colorTag} 评分: {score.TotalScore:F3}");
                        preview.AppendLine($"    来源: {source} | 类型: {memory.TypeName}");
                        preview.AppendLine($"    ├─ 重要性: {score.ImportanceScore:F3}");
                        preview.AppendLine($"    ├─ 关键词: {score.KeywordScore:F3}");
                        preview.AppendLine($"    ├─ 时间: {score.TimeScore:F3} (SCM/ELS不计时间)");
                        preview.AppendLine($"    └─ 加成: {score.BonusScore:F3} (层级+固定+编辑)");
                        preview.AppendLine($"    内容: \"{memory.content}\"");
                        preview.AppendLine();
                    }
                }
                else
                {
                    preview.AppendLine("⚠️ 没有记忆达到阈值，返回 null (不注入记忆)");
                    preview.AppendLine($"📊 当前阈值: {settings.memoryScoreThreshold:F2}");
                    preview.AppendLine();
                }

                preview.AppendLine();
                
                // ===== 常识注入详细分析 =====
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine("🎓 【ExpandMemory - 常识库注入详细分析】");
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine();

                // ⭐ 显示常识注入统计和详细信息
                if (cachedAllKnowledgeScores != null && cachedAllKnowledgeScores.Count > 0)
                {
                    var selectedKnowledge = cachedAllKnowledgeScores.Where(s => s.FailReason != null && s.FailReason.StartsWith("Selected")).ToList();
                    var rejectedKnowledge = cachedAllKnowledgeScores.Where(s => s.FailReason == null || !s.FailReason.StartsWith("Selected")).ToList();
                    
                    preview.AppendLine($"🎯 已注入: {selectedKnowledge.Count} 条");
                    preview.AppendLine($"❌ 未注入: {rejectedKnowledge.Count} 条");
                    preview.AppendLine($"📊 评分阈值: {settings.knowledgeScoreThreshold:F2}");
                    preview.AppendLine();
                    
                    // 显示已注入的常识
                    preview.AppendLine("【✅ 已注入的常识】");
                    preview.AppendLine();
                    
                    foreach (var detail in selectedKnowledge.OrderByDescending(d => d.TotalScore))
                    {
                        string matchIcon = detail.MatchType == KnowledgeMatchType.Keyword ? "🔑" : "🧠";
                        string matchLabel = detail.MatchType == KnowledgeMatchType.Keyword ? "关键词" : "向量";
                        
                        preview.AppendLine($"{matchIcon} [{matchLabel}] 总分: {detail.TotalScore:F3}");
                        preview.AppendLine($"    标签: [{detail.Entry.tag}]");
                        preview.AppendLine($"    ├─ 基础重要性: {detail.BaseScore:F2}");
                        preview.AppendLine($"    └─ 匹配得分: {detail.MatchTypeScore:F2}");
                        preview.AppendLine($"    内容: \"{detail.Entry.content}\"");
                        preview.AppendLine();
                    }
                    
                    // 显示未注入的常识（可选）
                    if (showRejectedKnowledge && rejectedKnowledge.Count > 0)
                    {
                        preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                        preview.AppendLine("【❌ 未注入的常识】");
                        preview.AppendLine();
                        
                        foreach (var detail in rejectedKnowledge.OrderByDescending(d => d.TotalScore).Take(10))
                        {
                            string matchIcon = detail.MatchType == KnowledgeMatchType.Keyword ? "🔑" : "🧠";
                            string matchLabel = detail.MatchType == KnowledgeMatchType.Keyword ? "关键词" : "向量";
                            string reasonLabel = GetFailReasonLabel(detail.FailReason);
                            
                            preview.AppendLine($"{matchIcon} [{matchLabel}] 总分: {detail.TotalScore:F3} | 原因: {reasonLabel}");
                            preview.AppendLine($"    标签: [{detail.Entry.tag}]");
                            preview.AppendLine($"    内容: \"{detail.Entry.content.Substring(0, Math.Min(60, detail.Entry.content.Length))}...\"");
                            preview.AppendLine();
                        }
                        
                        if (rejectedKnowledge.Count > 10)
                        {
                            preview.AppendLine($"    ... 还有 {rejectedKnowledge.Count - 10} 条未显示");
                        }
                        preview.AppendLine();
                    }
                }
                else if (knowledgeInjection != null && knowledgeScores != null)
                {
                    // 兼容旧版显示（如果没有 cachedAllKnowledgeScores）
                    preview.AppendLine($"🎯 动态评分选择了 {knowledgeScores.Count} 条常识");
                    preview.AppendLine($"📊 评分阈值: {settings.knowledgeScoreThreshold:F2} (低于此分数不注入)");
                    
                    // ⭐ 显示关键词数量
                    if (keywordInfo != null)
                    {
                        preview.AppendLine($"🔑 提取关键词: {keywordInfo.TotalKeywords} 个 (上下文 {keywordInfo.ContextKeywords.Count} + 角色 {keywordInfo.PawnKeywordsCount})");
                    }
                    preview.AppendLine();

                    for (int i = 0; i < knowledgeScores.Count; i++)
                    {
                        var score = knowledgeScores[i];
                        preview.AppendLine($"[{i + 1}] 📘 评分: {score.Score:F3}");
                        preview.AppendLine($"    标签: [{score.Entry.tag}]");
                        preview.AppendLine($"    重要性: {score.Entry.importance:F2}");
                        preview.AppendLine($"    内容: \"{score.Entry.content}\"");
                        preview.AppendLine();
                    }
                }
                else
                {
                    preview.AppendLine("⚠️ 没有常识达到阈值，返回 null (不注入常识)");
                    preview.AppendLine($"📊 当前阈值: {settings.knowledgeScoreThreshold:F2}");
                    
                    // ⭐ 显示关键词信息以帮助调试
                    if (keywordInfo != null)
                    {
                        preview.AppendLine($"🔑 已提取关键词: {keywordInfo.TotalKeywords} 个");
                        if (keywordInfo.ContextKeywords.Count > 0)
                        {
                            preview.AppendLine($"    前10个: {string.Join(", ", keywordInfo.ContextKeywords.Take(10))}");
                        }
                        else
                        {
                            preview.AppendLine("    ⚠️ 上下文关键词为空！请输入有效的上下文");
                        }
                    }
                    preview.AppendLine();
                }
                
                // ⭐ 新增：场景分析显示（使用实际上下文）
                if (!string.IsNullOrEmpty(contextInput))
                {
                    var sceneAnalysis = SceneAnalyzer.AnalyzeScene(contextInput);
                    var dynamicWeights = SceneAnalyzer.GetDynamicWeights(sceneAnalysis.PrimaryScene, sceneAnalysis.Confidence);
                    
                    preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                    preview.AppendLine("🎬 【场景分析】");
                    preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                    preview.AppendLine();
                    
                    // 场景类型和置信度
                    string sceneEmoji = GetSceneEmoji(sceneAnalysis.PrimaryScene);
                    string sceneDisplayName = SceneAnalyzer.GetSceneDisplayName(sceneAnalysis.PrimaryScene);
                    
                    preview.AppendLine($"{sceneEmoji} 【场景类型】: {sceneDisplayName}");
                    preview.AppendLine($"📊 【置信度】: {sceneAnalysis.Confidence:P0}");
                    preview.AppendLine();
                    
                    // 动态权重配置
                    preview.AppendLine("【动态权重配置】（用于记忆检索）:");
                    preview.AppendLine($"  • 时间衰减: {dynamicWeights.TimeDecay:F2} (越高越重视最近)");
                    preview.AppendLine($"  • 重要性: {dynamicWeights.Importance:F2}");
                    preview.AppendLine($"  • 关键词匹配: {dynamicWeights.KeywordMatch:F2}");
                    preview.AppendLine($"  • 关系加成: {dynamicWeights.RelationshipBonus:F2}");
                    preview.AppendLine($"  • 时间窗口: {dynamicWeights.RecencyWindow / 60000} 天");
                    preview.AppendLine();
                    
                    // 场景特性说明
                    preview.AppendLine("【场景特性】:");
                    preview.AppendLine(GetSceneCharacteristics(sceneAnalysis.PrimaryScene));
                    preview.AppendLine();
                    
                    // 多场景混合情况
                    if (sceneAnalysis.SceneScores.Count > 1)
                    {
                        preview.AppendLine("【场景混合情况】:");
                        foreach (var scoreKvp in sceneAnalysis.SceneScores.OrderByDescending(kvp => kvp.Value).Take(3))
                        {
                            string sceneName = SceneAnalyzer.GetSceneDisplayName(scoreKvp.Key);
                            preview.AppendLine($"  • {sceneName}: {scoreKvp.Value:P0}");
                        }
                        preview.AppendLine();
                    }
                    
                    preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                    preview.AppendLine();
                }


                // ===== 关键词提取详情 =====
                if (keywordInfo != null)
                {
                    preview.AppendLine();
                    preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                    preview.AppendLine("🔑 【关键词提取详情】");
                    preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                    preview.AppendLine();
                    
                    // 当前角色信息
                    preview.AppendLine($"【当前角色】: {selectedPawn.LabelShort}");
                    
                    // 目标角色信息
                    if (targetPawn != null)
                    {
                        preview.AppendLine($"【目标角色】: {targetPawn.LabelShort}");
                    }
                    
                    preview.AppendLine($"从上下文提取: {keywordInfo.ContextKeywords.Count} 个关键词");
                    preview.AppendLine($"从角色信息提取: {keywordInfo.PawnKeywordsCount} 个关键词");
                    preview.AppendLine($"总关键词: {keywordInfo.TotalKeywords} 个");
                    preview.AppendLine();
                    
                    // ⭐ 新增：显示具体的上下文关键词列表
                    if (keywordInfo.ContextKeywords.Count > 0)
                    {
                        preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                        preview.AppendLine("📝 【上下文关键词列表】");
                        preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                        preview.AppendLine();
                        
                        // 按长度分组显示
                        var grouped = keywordInfo.ContextKeywords
                            .GroupBy(kw => kw.Length)
                            .OrderByDescending(g => g.Key);
                        
                        foreach (var group in grouped)
                        {
                            preview.AppendLine($"【{group.Key}字关键词】 ({group.Count()}个):");
                            var keywords = group.OrderBy(kw => kw).Take(20).ToList(); // 每组最多显示20个
                            preview.AppendLine("  " + string.Join(", ", keywords));
                            if (group.Count() > 20)
                            {
                                preview.AppendLine($"  ... 还有 {group.Count() - 20} 个");
                            }
                            preview.AppendLine();
                        }
                    }
                    
                    // 显示PawnInfo（仅显示当前角色的详细信息）
                    if (keywordInfo.PawnInfo != null)
                    {
                        var pawnInfo = keywordInfo.PawnInfo;
                        preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                        preview.AppendLine($"【{pawnInfo.PawnName} 的关键词分类】");
                        preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                        preview.AppendLine();
                        
                        if (pawnInfo.NameKeywords.Count > 0)
                        {
                            preview.AppendLine($"👤 名字关键词 ({pawnInfo.NameKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.NameKeywords));
                            preview.AppendLine();
                        }
                        
                        if (pawnInfo.AgeKeywords.Count > 0)
                        {
                            preview.AppendLine($"🎂 年龄关键词 ({pawnInfo.AgeKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.AgeKeywords));
                            preview.AppendLine();
                        }
                        
                        if (pawnInfo.GenderKeywords.Count > 0)
                        {
                            preview.AppendLine($"⚥ 性别关键词 ({pawnInfo.GenderKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.GenderKeywords));
                            preview.AppendLine();
                        }
                        
                        if (pawnInfo.RaceKeywords.Count > 0)
                        {
                            preview.AppendLine($"🧬 种族关键词 ({pawnInfo.RaceKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.RaceKeywords));
                            preview.AppendLine();
                        }
                        
                        if (pawnInfo.TraitKeywords.Count > 0)
                        {
                            preview.AppendLine($"🎭 特质关键词 ({pawnInfo.TraitKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.TraitKeywords.Take(10)));
                            if (pawnInfo.TraitKeywords.Count > 10)
                                preview.AppendLine($"   ... 还有 {pawnInfo.TraitKeywords.Count - 10} 个");
                            preview.AppendLine();
                        }
                        
                        if (pawnInfo.SkillKeywords.Count > 0)
                        {
                            preview.AppendLine($"🛠️ 技能关键词 ({pawnInfo.SkillKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.SkillKeywords));
                            preview.AppendLine();
                        }
                        
                        if (pawnInfo.SkillLevelKeywords.Count > 0)
                        {
                            preview.AppendLine($"⭐ 技能等级关键词 ({pawnInfo.SkillLevelKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.SkillLevelKeywords.Distinct()));
                            preview.AppendLine();
                        }
                        
                        if (pawnInfo.HealthKeywords.Count > 0)
                        {
                            preview.AppendLine($"💚 健康状况关键词 ({pawnInfo.HealthKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.HealthKeywords.Distinct()));
                            preview.AppendLine();
                        }
                        
                        if (pawnInfo.RelationshipKeywords.Count > 0)
                        {
                            preview.AppendLine($"👥 关系网络关键词 ({pawnInfo.RelationshipKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.RelationshipKeywords.Take(10)));
                            if (pawnInfo.RelationshipKeywords.Count > 10)
                                preview.AppendLine($"   ... 还有 {pawnInfo.RelationshipKeywords.Count - 10} 个");
                            preview.AppendLine();
                        }
                        
                        if (pawnInfo.BackstoryKeywords.Count > 0)
                        {
                            preview.AppendLine($"📖 背景故事关键词 ({pawnInfo.BackstoryKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.BackstoryKeywords.Take(15)));
                            if (pawnInfo.BackstoryKeywords.Count > 15)
                                preview.AppendLine($"   ... 还有 {pawnInfo.BackstoryKeywords.Count - 15} 个");
                            preview.AppendLine();
                        }
                        
                        if (pawnInfo.ChildhoodKeywords.Count > 0)
                        {
                            preview.AppendLine($"🎈 童年背景关键词 ({pawnInfo.ChildhoodKeywords.Count}个)");
                            preview.AppendLine("   " + string.Join(", ", pawnInfo.ChildhoodKeywords.Take(15)));
                            if (pawnInfo.ChildhoodKeywords.Count > 15)
                                preview.AppendLine($"   ... 还有 {pawnInfo.ChildhoodKeywords.Count - 15} 个");
                            preview.AppendLine();
                        }
                    }
                    
                    // 如果有目标角色，显示提示信息
                    if (targetPawn != null)
                    {
                        preview.AppendLine($"💡 【提示】");
                        preview.AppendLine($"目标角色 {targetPawn.LabelShort} 的关键词已合并到总关键词池中");
                        preview.AppendLine($"用于常识匹配，但详细分类仅显示当前角色");
                        preview.AppendLine();
                    }
                }

                preview.AppendLine();
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine("📊 【注入统计】");
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine();
                preview.AppendLine($"✅ 记忆注入: {cachedMemoryCount} 条");
                preview.AppendLine($"✅ 常识注入: {cachedKnowledgeCount} 条");
                preview.AppendLine($"📦 总Token估算: ~{EstimateTokens(memoryInjection, knowledgeInjection)} tokens");
                preview.AppendLine($"💰 API成本估算: ~${EstimateCost(memoryInjection, knowledgeInjection):F4} (GPT-4)");
                preview.AppendLine();
                
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine("感谢使用 RimTalk 调试预览器！");
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine();
                
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine("💡 【颜色标注说明】");
                preview.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━══");
                preview.AppendLine();
                preview.AppendLine("🟦 [ABM] - 超短期记忆 (不会被注入，保留给 TalkHistory)");
                preview.AppendLine("🟨 [SCM] - 短期记忆 (近期事件，无时间加成)");
                preview.AppendLine("🟧 [ELS] - 中期记忆 (AI总结，无时间加成)");
                preview.AppendLine("🟪 [CLPA] - 长期记忆 (核心人设，有时间加成)");
                preview.AppendLine("📘 [常识] - 常识库条目 (世界观/背景知识)");
                preview.AppendLine();

                cachedPreview = preview.ToString();
                lastRefreshedContext = contextInput; // ⭐ 更新上次刷新的上下文
            }
            catch (Exception ex)
            {
                cachedPreview = $"生成预览时发生错误: {ex.Message}";
            }
        }

        /// <summary>
        /// ⭐ 新增：绘制上下文输入框
        /// </summary>
        private void DrawContextInput(Rect rect)
        {
            // 标签
            GUI.color = new Color(1f, 0.9f, 0.8f);
            Widgets.Label(new Rect(rect.x, rect.y, 120f, 30f), "上下文输入：");
            GUI.color = Color.white;
            
            // ⭐ 新增：测试向量匹配按钮
            Rect vectorTestButtonRect = new Rect(rect.x + rect.width - 310f, rect.y, 150f, 30f);
            if (Widgets.ButtonText(vectorTestButtonRect, "🧠 测试向量匹配"))
            {
                TestVectorMatching();
            }
            TooltipHandler.TipRegion(vectorTestButtonRect, "将上下文内容发送到向量库进行匹配测试\n可以在预览中看到向量检索的结果");
            
            // ⭐ 新增：读取上次RimTalk输入按钮
            Rect loadButtonRect = new Rect(rect.x + rect.width - 150f, rect.y, 140f, 30f);
            if (Widgets.ButtonText(loadButtonRect, "读取上次输入 📥"))
            {
                LoadLastRimTalkContext();
            }
            TooltipHandler.TipRegion(loadButtonRect, "从RimTalk读取最后一次发送给AI的对话内容\n（仅当RimTalk已安装且有对话记录时可用）");
            
            // 输入框 - 使用TextArea支持多行
            Rect textFieldRect = new Rect(rect.x + 130f, rect.y, rect.width - 470f, 60f);
            
            // ⭐ 修复：不要在输入时立即刷新，只更新输入内容
            contextInput = Widgets.TextArea(textFieldRect, contextInput);
            
            // 提示文字（如果为空）
            if (string.IsNullOrEmpty(contextInput))
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(textFieldRect.x + 5f, textFieldRect.y + 5f, textFieldRect.width - 10f, 40f), 
                    "输入对话上下文（例如：最近的对话内容、话题等）\n留空则仅基于重要性和层级评分");
                GUI.color = Color.white;
            }
        }
        
        /// <summary>
        /// ⭐ 新增：测试向量匹配功能
        /// </summary>
        private void TestVectorMatching()
        {
            if (string.IsNullOrEmpty(contextInput))
            {
                Messages.Message("请先输入上下文内容", MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            var settings = RimTalkMemoryPatchMod.Settings;
            if (!settings.enableVectorEnhancement)
            {
                Messages.Message("向量增强功能未启用，请在设置中开启", MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            try
            {
                // ⭐ 使用 ContextCleaner 清理上下文，去除噪音
                string cleanedContext = ContextCleaner.CleanForVectorMatching(contextInput);
                
                // 调用向量服务进行匹配
                var vectorResults = VectorDB.VectorService.Instance.FindBestLoreIds(
                    cleanedContext, 
                    settings.maxVectorResults * 2,  // 多获取一些结果
                    settings.vectorSimilarityThreshold
                );
                
                if (vectorResults == null || vectorResults.Count == 0)
                {
                    Messages.Message($"未找到相似度 >= {settings.vectorSimilarityThreshold:F2} 的常识", MessageTypeDefOf.NeutralEvent, false);
                    return;
                }
                
                // 显示结果统计
                Messages.Message($"找到 {vectorResults.Count} 条匹配的常识，刷新预览查看详情", MessageTypeDefOf.PositiveEvent, false);
                
                // 刷新预览以显示结果
                RefreshPreview();
                
                // 输出详细日志
                Log.Message($"[RimTalk-ExpandMemory] ========== 向量匹配测试 ==========");
                Log.Message($"[RimTalk-ExpandMemory] 原始上下文: {contextInput.Substring(0, Math.Min(100, contextInput.Length))}");
                Log.Message($"[RimTalk-ExpandMemory] 清理后上下文: {cleanedContext}");
                Log.Message($"[RimTalk-ExpandMemory] 阈值: {settings.vectorSimilarityThreshold:F2}");
                Log.Message($"[RimTalk-ExpandMemory] 结果数: {vectorResults.Count}");
                
                foreach (var (id, similarity) in vectorResults.Take(10))
                {
                    var library = MemoryManager.GetCommonKnowledge();
                    var entry = library.Entries.FirstOrDefault(e => e.id == id);
                    if (entry != null)
                    {
                        string preview = entry.content.Length > 50 ? entry.content.Substring(0, 50) + "..." : entry.content;
                        Log.Message($"[RimTalk-ExpandMemory]   [{entry.tag}] 相似度: {similarity:F4} - {preview}");
                    }
                }
                
                Log.Message($"[RimTalk-ExpandMemory] ========================================");
            }
            catch (Exception ex)
            {
                Messages.Message($"向量匹配失败: {ex.Message}", MessageTypeDefOf.RejectInput, false);
                Log.Error($"[RimTalk-ExpandMemory] Vector matching test failed: {ex}");
            }
        }

        /// <summary>
        /// ⭐ 新增：从RimTalk加载最后一次请求的上下文
        /// </summary>
        private void LoadLastRimTalkContext()
        {
            try
            {
                // 尝试通过API获取最后一次请求
                string lastContext = RimTalkMemoryAPI.GetLastRimTalkContext(
                    out Pawn lastPawn, 
                    out int lastTick
                );
                
                if (string.IsNullOrEmpty(lastContext))
                {
                    Messages.Message("未找到RimTalk的最近对话记录", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                
                // 计算距离上次请求的时间
                int currentTick = Find.TickManager.TicksGame;
                int ticksAgo = currentTick - lastTick;
                string timeAgo = ticksAgo < 60 ? "刚刚" : 
                                ticksAgo < 2500 ? $"{ticksAgo / 60}分钟前" : 
                                ticksAgo < 60000 ? $"{ticksAgo / 2500}小时前" : 
                                $"{ticksAgo / 60000}天前";
                
                // 设置上下文
                contextInput = lastContext;
                
                // 如果殖民者不同，也切换殖民者
                if (lastPawn != null && lastPawn != selectedPawn)
                {
                    selectedPawn = lastPawn;
                }
                
                // 清空缓存，标记需要刷新
                cachedPreview = "";
                RefreshPreview(); // 立即刷新预览
                
                // 显示成功消息
                string pawnName = lastPawn != null ? lastPawn.LabelShort : "未知";
                Messages.Message($"已加载 {pawnName} 的最后一次对话（{timeAgo}）", MessageTypeDefOf.PositiveEvent, false);
            }
            catch (Exception ex)
            {
                Messages.Message("读取失败：" + ex.Message, MessageTypeDefOf.RejectInput, false);
            }
        }
        
        /// <summary>
        /// ⭐ 新增：获取场景图标
        /// </summary>
        private string GetSceneEmoji(SceneType sceneType)
        {
            switch (sceneType)
            {
                case SceneType.Combat:
                    return "⚔️";
                case SceneType.Social:
                    return "💬";
                case SceneType.Work:
                    return "🔨";
                case SceneType.Medical:
                    return "💉";
                case SceneType.Research:
                    return "🔬";
                case SceneType.Event:
                    return "🎉";
                case SceneType.Neutral:
                default:
                    return "🏠";
            }
        }
        
        /// <summary>
        /// ⭐ 新增：获取场景特性说明
        /// </summary>
        private string GetSceneCharacteristics(SceneType sceneType)
        {
            switch (sceneType)
            {
                case SceneType.Combat:
                    return "  • 强调最近记忆（时间衰减0.8）\n" +
                           "  • 只关注重要事件（重要性0.5）\n" +
                           "  • 精准关键词匹配（0.4）\n" +
                           "  • 弱化关系因素（0.1）\n" +
                           "  • 时间窗口：6小时";
                case SceneType.Social:
                    return "  • 允许回忆旧事（时间衰减0.05）\n" +
                           "  • 小事也能聊（重要性0.2）\n" +
                           "  • 宽松匹配（关键词0.25）\n" +
                           "  • 强化共同记忆（关系0.6）\n" +
                           "  • 时间窗口：30天";
                case SceneType.Work:
                    return "  • 平衡时效性（时间衰减0.3）\n" +
                           "  • 中等重要性（0.3）\n" +
                           "  • 相关性优先（关键词0.35）\n" +
                           "  • 关系次要（0.15）\n" +
                           "  • 时间窗口：7天";
                case SceneType.Medical:
                    return "  • 重视医疗史（时间衰减0.15）\n" +
                           "  • 健康记录重要（重要性0.45）\n" +
                           "  • 精准匹配（关键词0.35）\n" +
                           "  • 关系适中（0.2）\n" +
                           "  • 时间窗口：14天";
                case SceneType.Research:
                    return "  • 知识积累（时间衰减0.02）\n" +
                           "  • 长期记忆（重要性0.4）\n" +
                           "  • 专业匹配（关键词0.4）\n" +
                           "  • 关系弱化（0.1）\n" +
                           "  • 时间窗口：60天";
                case SceneType.Event:
                    return "  • 永久记忆（时间衰减0.1）\n" +
                           "  • 重要时刻（重要性0.5）\n" +
                           "  • 事件相关（关键词0.3）\n" +
                           "  • 关系重要（0.4）\n" +
                           "  • 时间窗口：15天";
                case SceneType.Neutral:
                default:
                    return "  • 平衡配置（时间衰减0.25）\n" +
                           "  • 均衡权重（所有0.3）\n" +
                           "  • 通用场景\n" +
                           "  • 时间窗口：10天";
            }
        }
        
        /// <summary>
        /// ⭐ 新增：获取失败原因标签
        /// </summary>
        private string GetFailReasonLabel(string failReason)
        {
            if (failReason != null && failReason.StartsWith("Selected"))
                return "✅ 已选中";

            switch (failReason)
            {
                case "LowScore":
                    return "📉 分数过低";
                case "ConfidenceMargin":
                    return "🎯 领跑分过滤";
                case "ExceedMaxEntries":
                    return "📊 超出数量限制";
                case "Excluded":
                    return "🚫 被排除词过滤";
                case "Pending":
                    return "⏳ 待处理";
                default:
                    return "❓ 未知";
            }
        }
        
        private string GetMemorySourceTag(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return "ABM (不注入)";
                case MemoryLayer.Situational:
                    return "SCM (ExpandMemory)";
                case MemoryLayer.EventLog:
                    return "ELS (ExpandMemory)";
                case MemoryLayer.Archive:
                    return "CLPA (ExpandMemory)";
                default:
                    return "Unknown";
            }
        }
        
        private string GetMemoryColorTag(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return "🟦";
                case MemoryLayer.Situational:
                    return "🟨";
                case MemoryLayer.EventLog:
                    return "🟧";
                case MemoryLayer.Archive:
                    return "🟪";
                default:
                    return "⬜";
            }
        }
        
        private int EstimateTokens(string memoryText, string knowledgeText)
        {
            int total = 0;
            
            if (!string.IsNullOrEmpty(memoryText))
            {
                // 中文约 1.5 字符 = 1 token
                total += (int)(memoryText.Length / 1.5f);
            }
            
            if (!string.IsNullOrEmpty(knowledgeText))
            {
                total += (int)(knowledgeText.Length / 1.5f);
            }
            
            return total;
        }
        
        private float EstimateCost(string memoryText, string knowledgeText)
        {
            int tokens = EstimateTokens(memoryText, knowledgeText);
            // GPT-4 input cost: $0.03 per 1K tokens
            return tokens * 0.03f / 1000f;
        }
    }
}
