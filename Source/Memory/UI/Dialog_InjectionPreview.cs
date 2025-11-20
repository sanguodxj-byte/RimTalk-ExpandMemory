using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.Debug
{
    /// <summary>
    /// 注入内容预览器 - 显示即将注入到AI的记忆和常识
    /// </summary>
    public class Dialog_InjectionPreview : Window
    {
        private Pawn selectedPawn;
        private Vector2 scrollPosition;
        private string cachedPreview = "";
        private int cachedMemoryCount = 0;
        private int cachedKnowledgeCount = 0;

        public override Vector2 InitialSize => new Vector2(900f, 700f);

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
            Widgets.Label(new Rect(0f, yPos, 400f, 35f), "注入内容预览器");
            Text.Font = GameFont.Small;
            yPos += 40f;

            // 殖民者选择器
            DrawPawnSelector(new Rect(0f, yPos, inRect.width, 40f));
            yPos += 45f;

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

            // 统计信息
            DrawStats(new Rect(0f, yPos, inRect.width, 60f));
            yPos += 65f;

            // 刷新按钮
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

        private void DrawPawnSelector(Rect rect)
        {
            GUI.color = new Color(0.8f, 0.9f, 1f);
            Widgets.Label(new Rect(rect.x, rect.y, 100f, rect.height), "选择殖民者：");
            GUI.color = Color.white;

            Rect buttonRect = new Rect(rect.x + 110f, rect.y, 200f, 35f);
            
            string label = selectedPawn != null ? selectedPawn.LabelShort : "无";
            if (Widgets.ButtonText(buttonRect, label))
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                
                if (Find.CurrentMap != null)
                {
                    foreach (var pawn in Find.CurrentMap.mapPawns.FreeColonists)
                    {
                        Pawn localPawn = pawn;
                        options.Add(new FloatMenuOption(pawn.LabelShort, delegate
                        {
                            selectedPawn = localPawn;
                            cachedPreview = ""; // 清空缓存，强制刷新
                        }));
                    }
                }

                if (options.Count > 0)
                {
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }

            // 显示选中殖民者的基本信息
            if (selectedPawn != null)
            {
                GUI.color = Color.gray;
                string info = $"{selectedPawn.def.label}";
                if (selectedPawn.gender != null)
                    info += $" | {selectedPawn.gender.GetLabel()}";
                Widgets.Label(new Rect(rect.x + 320f, rect.y + 8f, 300f, rect.height), info);
                GUI.color = Color.white;
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

            // 第一行
            GUI.color = new Color(0.8f, 1f, 0.8f);
            Widgets.Label(new Rect(x, innerRect.y, 200f, lineHeight), "记忆统计：");
            GUI.color = Color.white;

            x += 100f;
            Widgets.Label(new Rect(x, innerRect.y, 150f, lineHeight), 
                $"ABM: {memoryComp.ActiveMemories.Count}");
            
            x += 120f;
            Widgets.Label(new Rect(x, innerRect.y, 150f, lineHeight), 
                $"SCM: {memoryComp.SituationalMemories.Count}");
            
            x += 120f;
            Widgets.Label(new Rect(x, innerRect.y, 150f, lineHeight), 
                $"ELS: {memoryComp.EventLogMemories.Count}");
            
            x += 120f;
            Widgets.Label(new Rect(x, innerRect.y, 150f, lineHeight), 
                $"CLPA: {memoryComp.ArchiveMemories.Count}");

            // 第二行
            x = innerRect.x;
            GUI.color = new Color(1f, 1f, 0.8f);
            Widgets.Label(new Rect(x, innerRect.y + lineHeight + 5f, 200f, lineHeight), "常识库统计：");
            GUI.color = Color.white;

            x += 100f;
            var library = MemoryManager.GetCommonKnowledge();
            int totalKnowledge = library.Entries.Count;
            int enabledKnowledge = library.Entries.Count(e => e.isEnabled);
            
            Widgets.Label(new Rect(x, innerRect.y + lineHeight + 5f, 300f, lineHeight), 
                $"总数: {totalKnowledge} | 启用: {enabledKnowledge}");

            // 第三行 - 当前配置
            x = innerRect.x;
            GUI.color = new Color(0.8f, 0.8f, 1f);
            Widgets.Label(new Rect(x, innerRect.y + lineHeight * 2 + 10f, 200f, lineHeight), "注入配置：");
            GUI.color = Color.white;

            x += 100f;
            var settings = RimTalkMemoryPatchMod.Settings;
            if (settings != null)
            {
                string mode = settings.useDynamicInjection ? "动态" : "静态";
                Widgets.Label(new Rect(x, innerRect.y + lineHeight * 2 + 10f, 500f, lineHeight), 
                    $"模式: {mode} | 记忆: {settings.maxInjectedMemories} | 常识: {settings.maxInjectedKnowledge}");
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
                preview.AppendLine("=".PadRight(80, '='));
                preview.AppendLine($"殖民者：{selectedPawn.LabelShort}");
                preview.AppendLine($"时间：{Find.TickManager.TicksGame.ToStringTicksToPeriod()}");
                preview.AppendLine($"注入模式：{(settings.useDynamicInjection ? "动态" : "静态")}");
                preview.AppendLine("=".PadRight(80, '='));
                preview.AppendLine();

                // 记忆部分
                preview.AppendLine("【记忆注入内容】");
                preview.AppendLine();

                string memoryContext;
                if (settings.useDynamicInjection)
                {
                    var injectedMemories = DynamicMemoryInjection.InjectMemoriesWithDetails(
                        memoryComp, 
                        "", // 不使用模拟上下文
                        settings.maxInjectedMemories,
                        out var memoryScores
                    );

                    cachedMemoryCount = memoryScores.Count;

                    if (memoryScores.Count > 0)
                    {
                        preview.AppendLine($"动态选择了 {memoryScores.Count} 条记忆（评分从高到低）：");
                        preview.AppendLine();

                        for (int i = 0; i < memoryScores.Count; i++)
                        {
                            var score = memoryScores[i];
                            preview.AppendLine($"[{i + 1}] 评分: {score.TotalScore:F2}");
                            preview.AppendLine($"    层级: {score.Memory.layer} | 时间: {score.Memory.Age:F1}小时前");
                            preview.AppendLine($"    ├ 时间衰减: {score.TimeScore:F2}");
                            preview.AppendLine($"    ├ 重要性: {score.ImportanceScore:F2}");
                            preview.AppendLine($"    ├ 关键词: {score.KeywordScore:F2}");
                            preview.AppendLine($"    └ 加成: {score.BonusScore:F2}");
                            preview.AppendLine($"    内容: {score.Memory.content}");
                            preview.AppendLine();
                        }

                        preview.AppendLine("注入的完整文本：");
                        preview.AppendLine("-".PadRight(80, '-'));
                        preview.AppendLine(injectedMemories);
                        preview.AppendLine("-".PadRight(80, '-'));
                    }
                    else
                    {
                        preview.AppendLine("没有可注入的记忆");
                    }
                }
                else
                {
                    // 静态注入
                    var allMemories = new System.Text.StringBuilder();
                    if (memoryComp.ActiveMemories.Count > 0)
                    {
                        allMemories.AppendLine("ABM (超短期):");
                        foreach (var m in memoryComp.ActiveMemories)
                            allMemories.AppendLine($"  - {m.content}");
                    }
                    if (memoryComp.SituationalMemories.Count > 0)
                    {
                        allMemories.AppendLine("SCM (短期):");
                        foreach (var m in memoryComp.SituationalMemories)
                            allMemories.AppendLine($"  - {m.content}");
                    }
                    
                    preview.AppendLine("静态注入（按层级顺序）：");
                    preview.AppendLine();
                    preview.AppendLine(allMemories.ToString());
                }

                preview.AppendLine();
                preview.AppendLine();

                // 常识部分
                preview.AppendLine("【常识注入内容】");
                preview.AppendLine();

                var library = MemoryManager.GetCommonKnowledge();
                var knowledgeWithScores = library.InjectKnowledgeWithDetails(
                    "", // 不使用模拟上下文
                    settings.maxInjectedKnowledge,
                    out var knowledgeScores
                );

                cachedKnowledgeCount = knowledgeScores.Count;

                if (knowledgeScores.Count > 0)
                {
                    preview.AppendLine($"动态选择了 {knowledgeScores.Count} 条常识（评分从高到低）：");
                    preview.AppendLine();

                    for (int i = 0; i < knowledgeScores.Count; i++)
                    {
                        var score = knowledgeScores[i];
                        preview.AppendLine($"[{i + 1}] 评分: {score.Score:F2}");
                        preview.AppendLine($"    标签: [{score.Entry.tag}]");
                        preview.AppendLine($"    重要性: {score.Entry.importance:F1}");
                        preview.AppendLine($"    内容: {score.Entry.content}");
                        preview.AppendLine();
                    }

                    preview.AppendLine("注入的完整文本：");
                    preview.AppendLine("-".PadRight(80, '-'));
                    preview.AppendLine(knowledgeWithScores);
                    preview.AppendLine("-".PadRight(80, '-'));
                }
                else
                {
                    preview.AppendLine("没有可注入的常识");
                }

                preview.AppendLine();
                preview.AppendLine("=".PadRight(80, '='));
                preview.AppendLine($"总结：注入了 {cachedMemoryCount} 条记忆 + {cachedKnowledgeCount} 条常识");
                preview.AppendLine("=".PadRight(80, '='));

                cachedPreview = preview.ToString();
            }
            catch (Exception ex)
            {
                cachedPreview = $"生成预览时出错：\n{ex.Message}\n\n{ex.StackTrace}";
                Log.Error($"[Injection Preview] Error: {ex}");
            }
        }
    }
}
