using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// 常识库管理窗口
    /// </summary>
    public class Dialog_CommonKnowledge : Window
    {
        private CommonKnowledgeLibrary library;
        private Vector2 scrollPosition;
        private string searchFilter = "";
        private CommonKnowledgeEntry selectedEntry = null;
        private bool editMode = false;
        
        // 自动生成折叠状态
        private bool expandAutoGenerate = false;
        
        // 编辑字段
        private string editTag = "";
        private string editContent = "";
        private float editImportance = 0.5f;
        private string editKeywords = "";
        // ? 新增：专属Pawn选择
        private int editTargetPawnId = -1;  // -1表示全局
        
        // 导入文本
        private string importText = "";

        public override Vector2 InitialSize => new Vector2(900f, 700f);

        public Dialog_CommonKnowledge(CommonKnowledgeLibrary library)
        {
            this.library = library;
            this.doCloseX = true;
            this.doCloseButton = true;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float yPos = 0f;

            // 工具栏
            Rect toolbarRect = new Rect(0f, yPos, inRect.width, 40f);
            DrawToolbar(toolbarRect);
            yPos += 45f;

            // 搜索框
            Rect searchRect = new Rect(0f, yPos, 300f, 30f);
            searchFilter = Widgets.TextField(searchRect, searchFilter);

            // 统计信息
            Rect statsRect = new Rect(310f, yPos, 300f, 30f);
            int enabledCount = library.Entries.Count(e => e.isEnabled);
            Widgets.Label(statsRect, "RimTalk_Knowledge_TotalAndEnabled".Translate(library.Entries.Count, enabledCount));
            yPos += 35f;

            // 自动生成常识折叠区
            Rect autoGenRect = new Rect(0f, yPos, inRect.width, 35f);
            DrawAutoGenerateSection(autoGenRect);
            yPos += expandAutoGenerate ? 140f : 40f;  // 30(标题) + 100(内容) + 10(间隔) = 140

            // 左侧列表
            Rect listRect = new Rect(0f, yPos, 450f, inRect.height - yPos - 50f);
            DrawEntryList(listRect);

            // 右侧详情/编辑区
            Rect detailRect = new Rect(460f, yPos, inRect.width - 460f, inRect.height - yPos - 50f);
            if (editMode)
                DrawEditPanel(detailRect);
            else
                DrawDetailPanel(detailRect);
        }

        private void DrawToolbar(Rect rect)
        {
            float buttonWidth = 100f;
            float spacing = 5f;
            float x = 0f;

            // 新建按钮
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "RimTalk_Knowledge_New".Translate()))
            {
                CreateNewEntry();
            }
            x += buttonWidth + spacing;

            // 导入按钮
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "RimTalk_Knowledge_Import".Translate()))
            {
                ShowImportDialog();
            }
            x += buttonWidth + spacing;

            // 导出按钮
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "RimTalk_Knowledge_Export".Translate()))
            {
                ExportToFile();
            }
            x += buttonWidth + spacing;

            // 删除按钮
            if (selectedEntry != null)
            {
                if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "RimTalk_Knowledge_Delete".Translate()))
                {
                    DeleteSelectedEntry();
                }
                x += buttonWidth + spacing;
            }

            // 清空按钮
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "RimTalk_Knowledge_ClearAll".Translate()))
            {
                ClearAllEntries();
            }
        }

        private void DrawAutoGenerateSection(Rect rect)
        {
            // 绘制折叠标题栏
            Rect headerRect = new Rect(rect.x, rect.y, rect.width, 30f);
            Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.3f, 0.4f, 0.5f));
            
            // 展开/折叠按钮
            Rect iconRect = new Rect(headerRect.x + 5f, headerRect.y + 5f, 20f, 20f);
            if (Widgets.ButtonImage(iconRect, expandAutoGenerate ? TexButton.Collapse : TexButton.Reveal))
            {
                expandAutoGenerate = !expandAutoGenerate;
            }
            
            // 标题
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 1f, 1f);
            Rect titleRect = new Rect(headerRect.x + 30f, headerRect.y + 5f, headerRect.width - 30f, headerRect.height);
            Widgets.Label(titleRect, "RimTalk_Knowledge_AutoGenerate".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            // 如果展开，显示内容
            if (expandAutoGenerate)
            {
                Rect contentRect = new Rect(rect.x + 10f, rect.y + 35f, rect.width - 20f, 100f);  // 30+35+35=100
                DrawAutoGenerateContent(contentRect);
            }
        }

        private void DrawAutoGenerateContent(Rect rect)
        {
            // 使用GUI.BeginGroup确保坐标系正确
            GUI.BeginGroup(rect);
            
            float yPos = 0f;
            float checkboxSize = 24f;
            float labelWidth = 200f;

            // 获取MOD设置
            var settings = RimTalkMemoryPatchMod.Settings;

            // 殖民者状态常识 - 复选框
            Rect pawnStatusCheckRect = new Rect(0f, yPos, checkboxSize, checkboxSize);
            bool pawnStatusEnabled = settings.enablePawnStatusKnowledge;
            Widgets.Checkbox(pawnStatusCheckRect.x, pawnStatusCheckRect.y, ref pawnStatusEnabled);
            settings.enablePawnStatusKnowledge = pawnStatusEnabled;
            
            // 标签
            Rect pawnStatusLabelRect = new Rect(checkboxSize + 5f, yPos, labelWidth, 25f);
            Widgets.Label(pawnStatusLabelRect, "RimTalk_Knowledge_PawnStatus".Translate());
            
            // 说明文字
            Rect pawnStatusDescRect = new Rect(checkboxSize + labelWidth + 10f, yPos + 3f, rect.width - checkboxSize - labelWidth - 10f, 20f);
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(pawnStatusDescRect, "RimTalk_Knowledge_PawnStatusDesc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            yPos += 30f;

            // 事件记录常识 - 复选框
            Rect eventRecordCheckRect = new Rect(0f, yPos, checkboxSize, checkboxSize);
            bool eventRecordEnabled = settings.enableEventRecordKnowledge;
            Widgets.Checkbox(eventRecordCheckRect.x, eventRecordCheckRect.y, ref eventRecordEnabled);
            settings.enableEventRecordKnowledge = eventRecordEnabled;
            
            // 标签
            Rect eventRecordLabelRect = new Rect(checkboxSize + 5f, yPos, labelWidth, 25f);
            Widgets.Label(eventRecordLabelRect, "RimTalk_Knowledge_EventRecord".Translate());
            
            // 说明文字
            Rect eventRecordDescRect = new Rect(checkboxSize + labelWidth + 10f, yPos + 3f, rect.width - checkboxSize - labelWidth - 10f, 20f);
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(eventRecordDescRect, "RimTalk_Knowledge_EventRecordDesc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            yPos += 35f;

            // ? 改用GUI.Button，确保正确渲染
            Rect generateNowButtonRect = new Rect(0f, yPos, 180f, 30f);
            if (GUI.Button(generateNowButtonRect, "RimTalk_Knowledge_GenerateNow".Translate()))
            {
                GenerateAllKnowledgeNow();
            }
            
            // 按钮说明
            Rect generateDescRect = new Rect(190f, yPos + 5f, rect.width - 190f, 20f);
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(generateDescRect, "RimTalk_Knowledge_GenerateNowDesc".Translate());
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            GUI.EndGroup();
        }

        /// <summary>
        /// 立即生成所有启用的常识类型
        /// </summary>
        private void GenerateAllKnowledgeNow()
        {
            var settings = RimTalkMemoryPatchMod.Settings;
            int totalGenerated = 0;

            if (settings.enablePawnStatusKnowledge)
            {
                GeneratePawnStatusKnowledge();
                totalGenerated++;
            }

            if (settings.enableEventRecordKnowledge)
            {
                GenerateEventRecordKnowledge();
                totalGenerated++;
            }

            if (totalGenerated == 0)
            {
                Messages.Message("RimTalk_Knowledge_EnableAtLeastOne".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }

        private void GeneratePawnStatusKnowledge()
        {
            if (Current.Game == null)
            {
                Messages.Message("RimTalk_Knowledge_MustEnterGame".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            int generatedCount = 0;
            int currentTick = Find.TickManager.TicksGame;

            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    try
                    {
                        // 计算入殖时长
                        // 注意：TimeAsColonistOrColonyAnimal 记录的是累计在殖民地的时间（ticks）
                        // 不是加入时的tick，所以直接使用这个值即可
                        int ticksInColony = pawn.records.GetAsInt(RecordDefOf.TimeAsColonistOrColonyAnimal);
                        int daysInColony = ticksInColony / GenDate.TicksPerDay;
                        
                        // 生成标签
                        string tag = $"殖民者,{pawn.LabelShort}";
                        
                        // 生成内容
                        var sb = new System.Text.StringBuilder();
                        sb.Append($"{pawn.LabelShort}");
                        
                        // 入殖时长
                        if (daysInColony < 5)
                        {
                            sb.Append("刚加入殖民地不久");
                        }
                        else if (daysInColony < 15)
                        {
                            sb.Append($"加入殖民地约{daysInColony}天");
                        }
                        else if (daysInColony < 60)
                        {
                            sb.Append($"是殖民地的老成员（{daysInColony}天）");
                        }
                        else
                        {
                            int years = daysInColony / 60;
                            sb.Append($"是殖民地的元老（{years}年{daysInColony % 60}天）");
                        }
                        
                        // 添加重要关系
                        if (pawn.relations != null)
                        {
                            var spouse = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse);
                            if (spouse != null)
                            {
                                sb.Append($"，配偶是{spouse.LabelShort}");
                            }
                            
                            var lover = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
                            if (lover != null && lover != spouse)
                            {
                                sb.Append($"，恋人是{lover.LabelShort}");
                            }
                        }
                        
                        string content = sb.ToString();
                        
                        // 检查是否已存在
                        bool exists = library.Entries.Any(e => 
                            e.tag.Contains(pawn.LabelShort) && 
                            e.content.Contains("殖民地") &&
                            (e.content.Contains("加入") || e.content.Contains("老成员") || e.content.Contains("元老"))
                        );
                        
                        if (!exists)
                        {
                            var entry = new CommonKnowledgeEntry(tag, content);
                            entry.importance = 0.6f;
                            library.AddEntry(entry);
                            generatedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"[CommonKnowledge] Error generating status for {pawn.LabelShort}: {ex.Message}");
                    }
                }
            }

            if (generatedCount > 0)
            {
                Messages.Message("RimTalk_Knowledge_GeneratedCount".Translate(generatedCount), MessageTypeDefOf.PositiveEvent, false);
                Log.Message($"[CommonKnowledge] Generated {generatedCount} pawn status knowledge entries");
            }
            else
            {
                Messages.Message("RimTalk_Knowledge_AllExist".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private void GenerateEventRecordKnowledge()
        {
            if (Current.Game == null)
            {
                Messages.Message("RimTalk_Knowledge_MustEnterGame".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            int generatedCount = 0;

            try
            {
                var gameHistory = Find.PlayLog;
                if (gameHistory != null)
                {
                    var recentEntries = gameHistory.AllEntries
                        .Where(e => e != null)
                        .OrderByDescending(e => e.Age)
                        .Take(100);

                    foreach (var logEntry in recentEntries)
                    {
                        try
                        {
                            string eventDesc = ExtractEventInfo(logEntry);
                            if (!string.IsNullOrEmpty(eventDesc))
                            {
                                bool exists = library.Entries.Any(e => 
                                    e.content.Contains(eventDesc.Substring(0, Math.Min(15, eventDesc.Length)))
                                );
                                
                                if (!exists)
                                {
                                    var entry = new CommonKnowledgeEntry("事件,历史", eventDesc);
                                    entry.importance = 0.6f;
                                    library.AddEntry(entry);
                                    generatedCount++;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Warning($"[CommonKnowledge] Error extracting event: {ex.Message}");
                        }
                    }
                }

                if (generatedCount > 0)
                {
                    Messages.Message("RimTalk_Knowledge_EventGeneratedCount".Translate(generatedCount), MessageTypeDefOf.PositiveEvent, false);
                    Log.Message($"[CommonKnowledge] Generated {generatedCount} event knowledge entries");
                }
                else
                {
                    Messages.Message("RimTalk_Knowledge_NoNewEvents".Translate(), MessageTypeDefOf.NeutralEvent, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledge] Error generating event knowledge: {ex.Message}\n{ex.StackTrace}");
                Messages.Message("RimTalk_Knowledge_EventGenerateFailed".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
            }
        }

        private string ExtractEventInfo(LogEntry logEntry)
        {
            if (logEntry == null) return null;

            try
            {
                // 检查是否为交互日志（特殊处理，避免POV错误）
                if (logEntry.GetType().Name == "PlayLogEntry_Interaction")
                {
                    // 跳过交互日志，这些已经由RimTalk对话记忆处理
                    return null;
                }
                
                string text = logEntry.ToGameStringFromPOV(null, false);
                if (string.IsNullOrEmpty(text)) return null;

                if (text.Length < 10 || text.Length > 200) return null;

                var boringKeywords = new[] { "走路", "吃饭", "睡觉", "娱乐", "闲逛" };
                if (boringKeywords.Any(k => text.Contains(k)))
                    return null;

                var importantKeywords = new[] 
                { 
                    "死亡", "死了", "被杀", "击杀",
                    "袭击", "进攻", "入侵",
                    "爆炸", "火灾", "崩溃",
                    "结婚", "订婚", "分手",
                    "加入", "逃跑", "离开",
                    "完成", "建造", "研究",
                    "贸易", "商队"
                };

                if (!importantKeywords.Any(k => text.Contains(k)))
                    return null;

                int currentTick = Find.TickManager.TicksGame;
                int ticksAgo = currentTick - logEntry.Age;
                int daysAgo = ticksAgo / GenDate.TicksPerDay;
                
                string timePrefix = "";
                if (daysAgo < 1)
                {
                    timePrefix = "今天";
                }
                else if (daysAgo < 5)
                {
                    timePrefix = $"{daysAgo}天前";
                }
                else if (daysAgo < 60)
                {
                    timePrefix = $"约{daysAgo}天前";
                }
                else
                {
                    return null;
                }

                return $"{timePrefix}{text}";
            }
            catch (Exception ex)
            {
                // 静默处理错误，避免日志污染
                if (Prefs.DevMode)
                {
                    Log.Warning($"[CommonKnowledge] Error in ExtractEventInfo: {ex.Message}");
                }
                return null;
            }
        }

        private void DrawEntryList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            
            var entries = library.Entries;
            
            if (!string.IsNullOrEmpty(searchFilter))
            {
                entries = entries.Where(e => 
                    (e.tag != null && e.tag.Contains(searchFilter)) ||
                    (e.content != null && e.content.Contains(searchFilter))
                ).ToList();
            }

            float viewHeight = entries.Count * 80f;
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, viewHeight);
            
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float y = 0f;
            foreach (var entry in entries)
            {
                Rect entryRect = new Rect(5f, y, viewRect.width - 10f, 75f);
                DrawEntryRow(entryRect, entry);
                y += 80f;
            }

            Widgets.EndScrollView();
        }

        private void DrawEntryRow(Rect rect, CommonKnowledgeEntry entry)
        {
            bool isSelected = selectedEntry == entry;
            
            if (isSelected)
            {
                Widgets.DrawHighlight(rect);
            }

            if (Widgets.ButtonInvisible(rect))
            {
                selectedEntry = entry;
                editMode = false;
            }

            Rect innerRect = rect.ContractedBy(5f);
            
            Rect checkboxRect = new Rect(innerRect.x, innerRect.y + 5f, 24f, 24f);
            bool wasEnabled = entry.isEnabled;
            Widgets.Checkbox(checkboxRect.x, checkboxRect.y, ref entry.isEnabled);

            Rect tagRect = new Rect(innerRect.x + 30f, innerRect.y, 120f, 25f);
            GUI.color = entry.isEnabled ? Color.white : Color.gray;
            Widgets.Label(tagRect, $"[{entry.tag}]");
            
            Rect contentRect = new Rect(innerRect.x + 30f, innerRect.y + 25f, innerRect.width - 30f, 40f);
            string preview = entry.content.Length > 60 ? entry.content.Substring(0, 60) + "..." : entry.content;
            Widgets.Label(contentRect, preview);
            
            GUI.color = Color.white;
        }

        private void DrawDetailPanel(Rect rect)
        {
            if (selectedEntry == null)
            {
                Widgets.Label(rect, "RimTalk_Knowledge_SelectEntry".Translate());
                return;
            }

            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            Rect innerRect = rect.ContractedBy(10f);
            
            float y = 0f;

            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "RimTalk_Knowledge_Tag".Translate());
            Widgets.Label(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), selectedEntry.tag);
            y += 30f;

            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "RimTalk_Knowledge_Importance".Translate());
            Widgets.Label(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), selectedEntry.importance.ToString("F2"));
            y += 30f;

            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "RimTalk_Knowledge_Status".Translate());
            Widgets.Label(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), selectedEntry.isEnabled ? "RimTalk_Knowledge_Enabled".Translate() : "RimTalk_Knowledge_Disabled".Translate());
            y += 30f;

            if (selectedEntry.keywords != null && selectedEntry.keywords.Any())
            {
                Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 25f), "RimTalk_Knowledge_Keywords".Translate());
                y += 25f;
                
                string keywordsText = string.Join(", ", selectedEntry.keywords.Take(10));
                Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 50f), keywordsText);
                y += 55f;
            }

            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 25f), "RimTalk_Knowledge_Content".Translate());
            y += 25f;
            
            Rect contentRect = new Rect(innerRect.x, y, innerRect.width, innerRect.height - y - 50f);
            Widgets.TextArea(contentRect, selectedEntry.content, true);

            Rect editButtonRect = new Rect(innerRect.x, innerRect.yMax - 40f, 100f, 35f);
            if (Widgets.ButtonText(editButtonRect, "RimTalk_Knowledge_Edit".Translate()))
            {
                StartEdit(selectedEntry);
            }
        }

        private void DrawEditPanel(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            Rect innerRect = rect.ContractedBy(10f);
            
            float y = 0f;

            // 标签
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "RimTalk_Knowledge_Tag".Translate());
            editTag = Widgets.TextField(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), editTag);
            y += 30f;

            // 重要性
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), string.Format("{0}: {1:F2}", "RimTalk_Knowledge_Importance".Translate(), editImportance));
            editImportance = Widgets.HorizontalSlider(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), editImportance, 0f, 1f);
            y += 35f;

            // ? 新增：专属Pawn选择
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "专属角色");
            Rect pawnButtonRect = new Rect(innerRect.x + 100f, y, 200f, 25f);
            
            string pawnLabel = "全局（所有人）";
            if (editTargetPawnId != -1)
            {
                // 查找Pawn
                Pawn targetPawn = FindPawnById(editTargetPawnId);
                pawnLabel = targetPawn != null ? $"专属: {targetPawn.LabelShort}" : "全局（所有人）";
            }
            
            if (Widgets.ButtonText(pawnButtonRect, pawnLabel))
            {
                ShowPawnSelectionMenu();
            }
            
            // 提示信息
            Rect hintRect = new Rect(innerRect.x + 310f, y + 3f, innerRect.width - 310f, 20f);
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(hintRect, "专属常识只会注入给指定角色");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 30f;

            // 关键词
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 25f), "RimTalk_Knowledge_KeywordsHint".Translate());
            y += 25f;
            Rect keywordsRect = new Rect(innerRect.x, y, innerRect.width, 25f);
            editKeywords = Widgets.TextField(keywordsRect, editKeywords);
            y += 30f;

            // 内容
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 25f), "RimTalk_Knowledge_Content".Translate());
            y += 25f;
            
            Rect contentRect = new Rect(innerRect.x, y, innerRect.width, innerRect.height - y - 50f);
            editContent = Widgets.TextArea(contentRect, editContent);

            // 按钮
            Rect buttonRect = new Rect(innerRect.x, innerRect.yMax - 40f, 100f, 35f);
            
            if (Widgets.ButtonText(buttonRect, "RimTalk_Knowledge_Save".Translate()))
            {
                SaveEdit();
            }
            
            buttonRect.x += 110f;
            if (Widgets.ButtonText(buttonRect, "RimTalk_Knowledge_Cancel".Translate()))
            {
                editMode = false;
            }
        }

        /// <summary>
        /// ? 新增：显示Pawn选择菜单
        /// </summary>
        private void ShowPawnSelectionMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            // 全局选项
            options.Add(new FloatMenuOption("全局（所有人）", delegate 
            { 
                editTargetPawnId = -1; 
            }));
            
            // 分隔线
            options.Add(new FloatMenuOption("—————————", null));
            
            // 所有殖民者
            if (Current.Game != null)
            {
                foreach (var map in Find.Maps)
                {
                    foreach (var pawn in map.mapPawns.FreeColonists.OrderBy(p => p.LabelShort))
                    {
                        Pawn p = pawn; // 闭包捕获
                        options.Add(new FloatMenuOption($"{p.LabelShort} (ID:{p.thingIDNumber})", delegate 
                        { 
                            editTargetPawnId = p.thingIDNumber; 
                        }));
                    }
                }
            }
            
            if (options.Count == 2) // 只有全局和分隔线
            {
                Messages.Message("当前没有殖民者", MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }
        
        /// <summary>
        /// ? 新增：根据ID查找Pawn
        /// </summary>
        private Pawn FindPawnById(int pawnId)
        {
            if (Current.Game == null || pawnId == -1) return null;
            
            foreach (var map in Find.Maps)
            {
                var pawn = map.mapPawns.AllPawns.FirstOrDefault(p => p.thingIDNumber == pawnId);
                if (pawn != null) return pawn;
            }
            
            return null;
        }

        private void CreateNewEntry()
        {
            editMode = true;
            editTag = "RimTalk_Knowledge_NewTag".Translate();
            editContent = "";
            editImportance = 0.5f;
            editKeywords = "";
            selectedEntry = null;
        }

        private void StartEdit(CommonKnowledgeEntry entry)
        {
            editMode = true;
            editTag = entry.tag;
            editContent = entry.content;
            editImportance = entry.importance;
            editKeywords = string.Join(", ", entry.keywords);
            editTargetPawnId = entry.targetPawnId; // ? 加载专属Pawn ID
        }

        private void SaveEdit()
        {
            if (string.IsNullOrEmpty(editContent))
            {
                Messages.Message("RimTalk_Knowledge_ContentRequired".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (selectedEntry != null)
            {
                selectedEntry.tag = editTag;
                selectedEntry.content = editContent;
                selectedEntry.importance = editImportance;
                selectedEntry.targetPawnId = editTargetPawnId; // ? 保存专属Pawn ID
                
                if (!string.IsNullOrWhiteSpace(editKeywords))
                {
                    selectedEntry.keywords = editKeywords
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();
                }
                else
                {
                    selectedEntry.keywords.Clear();
                }
                
                selectedEntry.GetTags();
                
                // ? 标记为用户编辑
                selectedEntry.isUserEdited = true;
            }
            else
            {
                var newEntry = new CommonKnowledgeEntry(editTag, editContent);
                newEntry.importance = editImportance;
                newEntry.targetPawnId = editTargetPawnId; // ? 设置专属Pawn ID
                newEntry.isUserEdited = true; // ? 标记为用户创建
                
                if (!string.IsNullOrWhiteSpace(editKeywords))
                {
                    newEntry.keywords = editKeywords
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();
                }

                library.AddEntry(newEntry);
                selectedEntry = newEntry;
            }

            editMode = false;
            Messages.Message("RimTalk_Knowledge_SaveSuccess".Translate(), MessageTypeDefOf.PositiveEvent, false);
        }

        private void DeleteSelectedEntry()
        {
            if (selectedEntry == null)
                return;

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk_Knowledge_DeleteConfirm".Translate(),
                delegate
                {
                    library.RemoveEntry(selectedEntry);
                    selectedEntry = null;
                    Messages.Message("RimTalk_Knowledge_Deleted".Translate(), MessageTypeDefOf.TaskCompletion, false);
                }
            ));
        }

        private void ClearAllEntries()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "RimTalk_Knowledge_ClearAllConfirm".Translate(library.Entries.Count),
                delegate
                {
                    library.Clear();
                    selectedEntry = null;
                    Messages.Message("RimTalk_Knowledge_Cleared".Translate(), MessageTypeDefOf.TaskCompletion, false);
                }
            ));
        }

        private void ShowImportDialog()
        {
            Dialog_TextInput dialog = new Dialog_TextInput(
                "RimTalk_Knowledge_ImportTitle".Translate(),
                "RimTalk_Knowledge_ImportDesc".Translate(),
                "",
                delegate(string text)
                {
                    int count = library.ImportFromText(text, false);
                    Messages.Message("RimTalk_Knowledge_ImportSuccess".Translate(count), MessageTypeDefOf.PositiveEvent, false);
                },
                null,
                multiline: true
            );
            
            Find.WindowStack.Add(dialog);
        }

        private void ExportToFile()
        {
            try
            {
                string exportText = library.ExportToText();
                
                if (string.IsNullOrEmpty(exportText))
                {
                    Messages.Message("RimTalk_Knowledge_NoContent".Translate(), MessageTypeDefOf.RejectInput, false);
                    return;
                }

                string fileName = $"CommonKnowledge_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(GenFilePaths.SaveDataFolderPath, fileName);
                
                File.WriteAllText(filePath, exportText);
                
                Messages.Message("RimTalk_Knowledge_ExportSuccess".Translate(filePath), MessageTypeDefOf.PositiveEvent, false);
                Application.OpenURL(GenFilePaths.SaveDataFolderPath);
            }
            catch (Exception ex)
            {
                Log.Error($"导出常识库失败: {ex}");
                Messages.Message("RimTalk_Knowledge_ExportFailed".Translate(ex.Message), MessageTypeDefOf.RejectInput, false);
            }
        }
    }

    public class Dialog_TextInput : Window
    {
        private string title;
        private string description;
        private string text;
        private Action<string> onAccept;
        private Action onCancel;
        private bool multiline;

        public override Vector2 InitialSize => new Vector2(600f, multiline ? 500f : 250f);

        public Dialog_TextInput(string title, string description, string initialText, Action<string> onAccept, Action onCancel = null, bool multiline = false)
        {
            this.title = title;
            this.description = description;
            this.text = initialText ?? "";
            this.onAccept = onAccept;
            this.onCancel = onCancel;
            this.multiline = multiline;
            
            this.doCloseX = true;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), title);
            Text.Font = GameFont.Small;

            float y = 40f;
            
            if (!string.IsNullOrEmpty(description))
            {
                float descHeight = Text.CalcHeight(description, inRect.width);
                Widgets.Label(new Rect(0f, y, inRect.width, descHeight), description);
                y += descHeight + 10f;
            }

            Rect textRect = new Rect(0f, y, inRect.width, inRect.height - y - 50f);
            if (multiline)
            {
                text = Widgets.TextArea(textRect, text);
            }
            else
            {
                text = Widgets.TextField(textRect, text);
            }

            Rect buttonRect = new Rect(inRect.width - 220f, inRect.height - 40f, 100f, 35f);
            if (Widgets.ButtonText(buttonRect, "RimTalk_Knowledge_Confirm".Translate()))
            {
                onAccept?.Invoke(text);
                Close();
            }

            buttonRect.x += 110f;
            if (Widgets.ButtonText(buttonRect, "RimTalk_Knowledge_Cancel".Translate()))
            {
                onCancel?.Invoke();
                Close();
            }
        }
    }
}
