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
        private int editTargetPawnId = -1;
        
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
            yPos += expandAutoGenerate ? 170f : 40f; // ? 从140f增加到170f（130+35+5）

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
            
            // ? v3.3.2.25: 向量注入按钮已禁用（VectorDB已移除）
            // 保留UI框架，未来可以重新启用语义搜索功能
            /*
            float vectorButtonWidth = buttonWidth + 20f;
            float vectorButtonX = rect.width - vectorButtonWidth;
            if (Widgets.ButtonText(new Rect(vectorButtonX, 0f, vectorButtonWidth, 35f), "注入向量库"))
            {
                ShowVectorDBInjectionDialog();
            }
            */
        }

        private void DrawAutoGenerateSection(Rect rect)
        {
            string sectionTitle = "自动生成";
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, rect.width, 30f), expandAutoGenerate ? $"▼ {sectionTitle}" : $"? {sectionTitle}"))
            {
                expandAutoGenerate = !expandAutoGenerate;
            }

            if (expandAutoGenerate)
            {
                Rect contentRect = new Rect(rect.x + 10f, rect.y + 35f, rect.width - 20f, 130f); // ? 从100增加到130
                GUI.Box(contentRect, "");
                
                Rect innerRect = contentRect.ContractedBy(5f);
                float y = innerRect.y;
                float buttonWidth = 120f;
                
                var settings = RimTalkMemoryPatchMod.Settings;
                
                // ? 第一行：Pawn状态常识
                Rect pawnStatusLineRect = new Rect(innerRect.x, y, innerRect.width, 30f);
                
                // 复选框
                Rect pawnCheckboxRect = new Rect(pawnStatusLineRect.x, pawnStatusLineRect.y, pawnStatusLineRect.width - buttonWidth - 10f, 30f);
                bool enablePawnStatus = settings.enablePawnStatusKnowledge;
                Widgets.CheckboxLabeled(pawnCheckboxRect, "生成Pawn状态常识", ref enablePawnStatus);
                settings.enablePawnStatusKnowledge = enablePawnStatus;
                
                // ? 立即生成按钮
                Rect pawnButtonRect = new Rect(pawnStatusLineRect.xMax - buttonWidth, pawnStatusLineRect.y, buttonWidth, 30f);
                if (Widgets.ButtonText(pawnButtonRect, "立即生成"))
                {
                    GeneratePawnStatusKnowledge();
                }
                
                y += 35f;
                
                // ? 第二行：事件记录常识
                Rect eventLineRect = new Rect(innerRect.x, y, innerRect.width, 30f);
                
                // 复选框
                Rect eventCheckboxRect = new Rect(eventLineRect.x, eventLineRect.y, eventLineRect.width - buttonWidth - 10f, 30f);
                bool enableEventRecord = settings.enableEventRecordKnowledge;
                Widgets.CheckboxLabeled(eventCheckboxRect, "生成事件记录常识", ref enableEventRecord);
                settings.enableEventRecordKnowledge = enableEventRecord;
                
                // ? 立即生成按钮
                Rect eventButtonRect = new Rect(eventLineRect.xMax - buttonWidth, eventLineRect.y, buttonWidth, 30f);
                if (Widgets.ButtonText(eventButtonRect, "立即生成"))
                {
                    GenerateEventRecordKnowledge();
                }
                
                y += 35f;
                
                // ? 提示信息
                GUI.color = Color.gray;
                Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 20f), "提示：启用自动生成后，系统会定期更新常识库");
                GUI.color = Color.white;
            }
        }
        
        /// <summary>
        /// 生成Pawn状态常识
        /// </summary>
        private void GeneratePawnStatusKnowledge()
        {
            try
            {
                var settings = RimTalkMemoryPatchMod.Settings;
                if (!settings.enablePawnStatusKnowledge)
                {
                    Messages.Message("Pawn状态常识生成未启用，请在设置中启用", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                
                // 获取所有殖民者
                var colonists = Find.CurrentMap?.mapPawns?.FreeColonists;
                if (colonists == null || colonists.Count() == 0)
                {
                    Messages.Message("当前地图没有殖民者", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                
                // 获取MemoryManager（用于访问colonistJoinTicks）
                var memoryManager = Find.World?.GetComponent<MemoryManager>();
                if (memoryManager == null)
                {
                    Messages.Message("无法获取MemoryManager", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                
                int currentTick = Find.TickManager.TicksGame;
                var colonistJoinTicks = memoryManager.ColonistJoinTicks;
                
                int generated = 0;
                foreach (var pawn in colonists)
                {
                    try
                    {
                        // 使用PawnStatusKnowledgeGenerator的逻辑
                        PawnStatusKnowledgeGenerator.UpdatePawnStatusKnowledge(pawn, library, currentTick, colonistJoinTicks);
                        generated++;
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[CommonKnowledge] Failed to generate knowledge for {pawn.Name}: {ex.Message}");
                    }
                }
                
                Messages.Message($"成功为 {generated} 个殖民者生成状态常识", MessageTypeDefOf.PositiveEvent, false);
                Log.Message($"[CommonKnowledge] Generated {generated} pawn status knowledge entries");
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledge] Pawn status generation failed: {ex.Message}");
                Messages.Message($"生成失败：{ex.Message}", MessageTypeDefOf.RejectInput, false);
            }
        }
        
        /// <summary>
        /// 生成事件记录常识
        /// </summary>
        private void GenerateEventRecordKnowledge()
        {
            try
            {
                var settings = RimTalkMemoryPatchMod.Settings;
                if (!settings.enableEventRecordKnowledge)
                {
                    Messages.Message("事件记录常识生成未启用，请在设置中启用", MessageTypeDefOf.RejectInput, false);
                    return;
                }
                
                // 直接调用EventRecordKnowledgeGenerator的扫描方法
                // 这将自动从PlayLog提取最近事件并生成常识
                EventRecordKnowledgeGenerator.ScanRecentPlayLog();
                
                // 由于ScanRecentPlayLog是自动管理的，我们给用户一个提示
                Messages.Message("已触发事件记录扫描，常识将自动生成到库中", MessageTypeDefOf.PositiveEvent, false);
                Log.Message($"[CommonKnowledge] Triggered event record knowledge scan");
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledge] Event record generation failed: {ex.Message}");
                Messages.Message($"生成失败：{ex.Message}", MessageTypeDefOf.RejectInput, false);
            }
        }

        private void DrawEntryList(Rect rect)
        {
            GUI.Box(rect, "");
            
            var viewRect = new Rect(0f, 0f, rect.width - 16f, library.Entries.Count * 70f); // ? 从50增加到70
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float y = 0f;
            var filteredEntries = library.Entries.Where(e => 
                string.IsNullOrEmpty(searchFilter) || 
                e.tag.ToLower().Contains(searchFilter.ToLower()) ||
                e.content.ToLower().Contains(searchFilter.ToLower())
            ).ToList();

            foreach (var entry in filteredEntries)
            {
                Rect entryRect = new Rect(0f, y, viewRect.width, 65f); // ? 从45增加到65
                
                if (selectedEntry == entry)
                {
                    Widgets.DrawHighlight(entryRect);
                }
                
                if (Widgets.ButtonInvisible(entryRect))
                {
                    selectedEntry = entry;
                    editMode = false;
                }

                // 启用/禁用复选框
                Rect checkboxRect = new Rect(entryRect.x + 5f, entryRect.y + 10f, 24f, 24f);
                bool wasEnabled = entry.isEnabled;
                Widgets.Checkbox(checkboxRect.position, ref entry.isEnabled);
                
                // 标签
                Rect tagRect = new Rect(entryRect.x + 35f, entryRect.y + 5f, 100f, 20f);
                Widgets.Label(tagRect, $"[{entry.tag}]");
                
                // 重要性
                Rect importanceRect = new Rect(entryRect.x + 140f, entryRect.y + 5f, 60f, 20f);
                Widgets.Label(importanceRect, entry.importance.ToString("F1"));
                
                // 内容预览（两行显示）
                Rect contentRect = new Rect(entryRect.x + 35f, entryRect.y + 25f, entryRect.width - 40f, 35f); // ? 从15增加到35，允许两行
                Text.Font = GameFont.Tiny;
                string preview = entry.content.Length > 80 ? entry.content.Substring(0, 80) + "..." : entry.content; // ? 从50增加到80
                Widgets.Label(contentRect, preview);
                Text.Font = GameFont.Small;

                y += 70f; // ? 从50增加到70
            }

            Widgets.EndScrollView();
        }

        private void DrawEditPanel(Rect rect)
        {
            GUI.Box(rect, "");
            Rect innerRect = rect.ContractedBy(10f);
            
            float y = innerRect.y;
            
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 25f), "编辑常识");
            y += 30f;
            
            // 标签
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "标签:");
            editTag = Widgets.TextField(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), editTag);
            y += 30f;
            
            // 重要性
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "重要性:");
            editImportance = Widgets.HorizontalSlider(new Rect(innerRect.x + 100f, y, innerRect.width - 150f, 25f), editImportance, 0f, 1f);
            Widgets.Label(new Rect(innerRect.x + innerRect.width - 40f, y, 40f, 25f), editImportance.ToString("F1"));
            y += 30f;
            
            // 内容
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "内容:");
            y += 30f;
            Rect contentRect = new Rect(innerRect.x, y, innerRect.width, 150f);
            editContent = Widgets.TextArea(contentRect, editContent);
            y += 160f;
            
            // 按钮
            if (Widgets.ButtonText(new Rect(innerRect.x, y, 100f, 30f), "保存"))
            {
                SaveEntry();
            }
            
            if (Widgets.ButtonText(new Rect(innerRect.x + 110f, y, 100f, 30f), "取消"))
            {
                editMode = false;
            }
        }

        private void DrawDetailPanel(Rect rect)
        {
            GUI.Box(rect, "");
            
            if (selectedEntry == null)
            {
                Widgets.Label(rect.ContractedBy(10f), "选择一个常识条目以查看详情");
                return;
            }
            
            Rect innerRect = rect.ContractedBy(10f);
            float y = innerRect.y;
            
            // 标题
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 25f), $"[{selectedEntry.tag}] 详情");
            y += 35f;
            
            // 重要性
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "重要性:");
            Widgets.Label(new Rect(innerRect.x + 100f, y, 100f, 25f), selectedEntry.importance.ToString("F1"));
            y += 30f;
            
            // 启用状态
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "状态:");
            Widgets.Label(new Rect(innerRect.x + 100f, y, 100f, 25f), selectedEntry.isEnabled ? "启用" : "禁用");
            y += 30f;
            
            // 内容
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "内容:");
            y += 30f;
            Rect contentRect = new Rect(innerRect.x, y, innerRect.width, 150f);
            Widgets.Label(contentRect, selectedEntry.content);
            y += 160f;
            
            // 编辑按钮
            if (Widgets.ButtonText(new Rect(innerRect.x, y, 100f, 30f), "编辑"))
            {
                StartEdit();
            }
        }

        private void CreateNewEntry()
        {
            editTag = "";
            editContent = "";
            editImportance = 0.5f;
            selectedEntry = null;
            editMode = true;
        }

        private void StartEdit()
        {
            if (selectedEntry != null)
            {
                editTag = selectedEntry.tag;
                editContent = selectedEntry.content;
                editImportance = selectedEntry.importance;
                editMode = true;
            }
        }

        private void SaveEntry()
        {
            if (string.IsNullOrEmpty(editTag) || string.IsNullOrEmpty(editContent))
            {
                Messages.Message("标签和内容不能为空", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (selectedEntry == null)
            {
                // 新建
                var newEntry = new CommonKnowledgeEntry(editTag, editContent)
                {
                    importance = editImportance
                };
                library.AddEntry(newEntry);
                selectedEntry = newEntry;
            }
            else
            {
                // 编辑现有
                selectedEntry.tag = editTag;
                selectedEntry.content = editContent;
                selectedEntry.importance = editImportance;
            }

            editMode = false;
            Messages.Message("常识已保存", MessageTypeDefOf.PositiveEvent, false);
        }

        private void ShowImportDialog()
        {
            Dialog_TextInput dialog = new Dialog_TextInput(
                "导入常识",
                "每行格式：[标签|重要性]内容\n例如：[世界观|0.9]这是边缘世界",
                "",
                delegate(string text)
                {
                    int count = library.ImportFromText(text);
                    Messages.Message($"已导入 {count} 条常识", MessageTypeDefOf.PositiveEvent, false);
                },
                null,
                true
            );
            
            Find.WindowStack.Add(dialog);
        }

        private void ExportToFile()
        {
            string content = library.ExportToText();
            GUIUtility.systemCopyBuffer = content;
            Messages.Message("常识库已复制到剪贴板", MessageTypeDefOf.PositiveEvent, false);
        }

        private void DeleteSelectedEntry()
        {
            if (selectedEntry != null)
            {
                library.RemoveEntry(selectedEntry);
                selectedEntry = null;
                Messages.Message("常识已删除", MessageTypeDefOf.PositiveEvent, false);
            }
        }

        private void ClearAllEntries()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "确定要清空所有常识吗？",
                delegate
                {
                    library.Clear();
                    selectedEntry = null;
                    Messages.Message("常识库已清空", MessageTypeDefOf.PositiveEvent, false);
                }
            ));
        }

        // ⭐ v3.3.2.25: 向量库相关方法已移除（VectorDB已禁用）
    }
    
    /// <summary>
    /// 向量库注入对话框 - 支持文本输入和文件导入
    /// </summary>
    public class Dialog_VectorDBInjection : Window
    {
        private Dialog_CommonKnowledge parentDialog;
        private string inputText = "";
        private Vector2 scrollPos = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(700f, 600f);

        public Dialog_VectorDBInjection(Dialog_CommonKnowledge parent)
        {
            this.parentDialog = parent;
            this.doCloseX = true;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "注入向量数据库");
            Text.Font = GameFont.Small;

            float y = 40f;
            
            // 说明文本
            GUI.color = new Color(0.8f, 0.9f, 1f);
            string description = "支持两种模式：\n\n" +
                "【格式模式】每行格式：[标签|重要性]内容\n" +
                "【纯文本模式】直接粘贴任意文本，自动分段处理\n\n" +
                "示例格式模式：\n[世界观|0.9]边缘世界，科技倒退\n\n" +
                "示例纯文本模式：\n这是边缘世界\n科技已经倒退\n海盗经常袭击";
            float descHeight = Text.CalcHeight(description, inRect.width);
            Widgets.Label(new Rect(0f, y, inRect.width, descHeight), description);
            GUI.color = Color.white;
            y += descHeight + 10f;

            // ? 文件导入按钮
            Rect fileButtonRect = new Rect(0f, y, 150f, 35f);
            if (Widgets.ButtonText(fileButtonRect, "?? 从TXT文件导入"))
            {
                ImportFromFile();
            }
            
            Rect clearButtonRect = new Rect(160f, y, 100f, 35f);
            if (Widgets.ButtonText(clearButtonRect, "清空"))
            {
                inputText = "";
            }
            y += 40f;

            // 文本输入区域
            Rect textRect = new Rect(0f, y, inRect.width, inRect.height - y - 90f);
            Rect viewRect = new Rect(0f, 0f, textRect.width - 16f, Mathf.Max(Text.CalcHeight(inputText, textRect.width - 16f), textRect.height));
            Widgets.BeginScrollView(textRect, ref scrollPos, viewRect);
            inputText = Widgets.TextArea(viewRect, inputText);
            Widgets.EndScrollView();

            // 底部按钮
            Rect confirmRect = new Rect(inRect.width - 220f, inRect.height - 40f, 100f, 35f);
            if (Widgets.ButtonText(confirmRect, "注入"))
            {
                InjectToVectorDB();
            }

            Rect cancelRect = new Rect(inRect.width - 110f, inRect.height - 40f, 100f, 35f);
            if (Widgets.ButtonText(cancelRect, "取消"))
            {
                Close();
            }
        }

        /// <summary>
        /// ? 从TXT文件导入
        /// </summary>
        private void ImportFromFile()
        {
            try
            {
                // RimWorld不支持标准文件对话框，使用简单的路径输入
                Find.WindowStack.Add(new Dialog_TextInput(
                    "输入TXT文件路径",
                    "请输入完整的文件路径，例如：\nC:\\Users\\YourName\\Documents\\knowledge.txt\n\n或者放在游戏目录下：\nMods\\YourMod\\knowledge.txt",
                    "",
                    delegate(string filePath)
                    {
                        LoadFileContent(filePath);
                    },
                    null,
                    false
                ));
            }
            catch (Exception ex)
            {
                Messages.Message($"文件导入失败：{ex.Message}", MessageTypeDefOf.RejectInput, false);
                Log.Error($"[VectorDB] File import error: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载文件内容
        /// </summary>
        private void LoadFileContent(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    Messages.Message("文件路径为空", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                // 支持相对路径（相对于游戏根目录）
                if (!System.IO.Path.IsPathRooted(filePath))
                {
                    filePath = System.IO.Path.Combine(UnityEngine.Application.dataPath, "..", filePath);
                }

                if (!System.IO.File.Exists(filePath))
                {
                    Messages.Message($"文件不存在：{filePath}", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                // 读取文件内容
                inputText = System.IO.File.ReadAllText(filePath, System.Text.Encoding.UTF8);
                Messages.Message($"成功加载文件：{System.IO.Path.GetFileName(filePath)}", MessageTypeDefOf.PositiveEvent, false);
                
                Log.Message($"[VectorDB] Loaded {inputText.Length} characters from {filePath}");
            }
            catch (Exception ex)
            {
                Messages.Message($"读取文件失败：{ex.Message}", MessageTypeDefOf.RejectInput, false);
                Log.Error($"[VectorDB] File read error: {ex.Message}");
            }
        }

        /// <summary>
        /// 注入到向量数据库
        /// </summary>
        private void InjectToVectorDB()
        {
            if (parentDialog != null)
            {
                // 调用父对话框的注入方法
                try
                {
                    var method = parentDialog.GetType().GetMethod("InjectTextToVectorDB", 
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    
                    if (method != null)
                    {
                        method.Invoke(parentDialog, new object[] { inputText });
                        Close();
                    }
                    else
                    {
                        Messages.Message("无法找到注入方法", MessageTypeDefOf.RejectInput, false);
                    }
                }
                catch (Exception ex)
                {
                    Messages.Message($"注入失败：{ex.Message}", MessageTypeDefOf.RejectInput, false);
                    Log.Error($"[VectorDB] Injection error: {ex.Message}");
                }
            }
        }
    }
    
    /// <summary>
    /// 简单的文本输入对话框
    /// </summary>
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
            if (Widgets.ButtonText(buttonRect, "确认"))
            {
                onAccept?.Invoke(text);
                Close();
            }

            buttonRect.x += 110f;
            if (Widgets.ButtonText(buttonRect, "取消"))
            {
                onCancel?.Invoke();
                Close();
            }
        }
    }
}

