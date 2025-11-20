using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

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
        
        // 编辑字段
        private string editTag = "";
        private string editContent = "";
        private float editImportance = 0.5f;
        private string editKeywords = ""; // 新增：关键词编辑字段
        
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

            // 工具栏 - 直接从顶部开始
            Rect toolbarRect = new Rect(0f, yPos, inRect.width, 40f);
            DrawToolbar(toolbarRect);
            yPos += 45f;

            // 搜索框
            Rect searchRect = new Rect(0f, yPos, 300f, 30f);
            searchFilter = Widgets.TextField(searchRect, searchFilter);

            // 统计信息
            Rect statsRect = new Rect(310f, yPos, 300f, 30f);
            int enabledCount = library.Entries.Count(e => e.isEnabled);
            Widgets.Label(statsRect, $"总数: {library.Entries.Count} | 启用: {enabledCount}");
            yPos += 35f;

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
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "新建"))
            {
                CreateNewEntry();
            }
            x += buttonWidth + spacing;

            // 导入按钮
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "导入"))
            {
                ShowImportDialog();
            }
            x += buttonWidth + spacing;

            // 导出按钮
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "导出"))
            {
                ExportToFile();
            }
            x += buttonWidth + spacing;

            // 删除按钮
            if (selectedEntry != null)
            {
                if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "删除"))
                {
                    DeleteSelectedEntry();
                }
                x += buttonWidth + spacing;
            }

            // 清空按钮
            if (Widgets.ButtonText(new Rect(x, 0f, buttonWidth, 35f), "清空全部"))
            {
                ClearAllEntries();
            }
        }

        private void DrawEntryList(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            
            var entries = library.Entries;
            
            // 应用搜索过滤
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
            
            // 启用复选框
            Rect checkboxRect = new Rect(innerRect.x, innerRect.y + 5f, 24f, 24f);
            bool wasEnabled = entry.isEnabled;
            Widgets.Checkbox(checkboxRect.x, checkboxRect.y, ref entry.isEnabled);
            
            if (entry.isEnabled != wasEnabled)
            {
                // 状态改变时的逻辑
            }

            // 标签
            Rect tagRect = new Rect(innerRect.x + 30f, innerRect.y, 120f, 25f);
            GUI.color = entry.isEnabled ? Color.white : Color.gray;
            Widgets.Label(tagRect, $"[{entry.tag}]");
            
            // 内容预览
            Rect contentRect = new Rect(innerRect.x + 30f, innerRect.y + 25f, innerRect.width - 30f, 40f);
            string preview = entry.content.Length > 60 ? entry.content.Substring(0, 60) + "..." : entry.content;
            Widgets.Label(contentRect, preview);
            
            GUI.color = Color.white;
        }

        private void DrawDetailPanel(Rect rect)
        {
            if (selectedEntry == null)
            {
                Widgets.Label(rect, "请选择一个常识条目");
                return;
            }

            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.3f));
            Rect innerRect = rect.ContractedBy(10f);
            
            float y = 0f;

            // 标签
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "标签:");
            Widgets.Label(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), selectedEntry.tag);
            y += 30f;

            // 重要性
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "重要性:");
            Widgets.Label(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), selectedEntry.importance.ToString("F2"));
            y += 30f;

            // 启用状态
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "状态:");
            Widgets.Label(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), selectedEntry.isEnabled ? "启用" : "禁用");
            y += 30f;

            // 关键词
            if (selectedEntry.keywords != null && selectedEntry.keywords.Any())
            {
                Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 25f), "关键词:");
                y += 25f;
                
                string keywordsText = string.Join(", ", selectedEntry.keywords.Take(10));
                Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 50f), keywordsText);
                y += 55f;
            }

            // 内容
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 25f), "内容:");
            y += 25f;
            
            Rect contentRect = new Rect(innerRect.x, y, innerRect.width, innerRect.height - y - 50f);
            Widgets.TextArea(contentRect, selectedEntry.content, true);

            // 编辑按钮
            Rect editButtonRect = new Rect(innerRect.x, innerRect.yMax - 40f, 100f, 35f);
            if (Widgets.ButtonText(editButtonRect, "编辑"))
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
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), "标签:");
            editTag = Widgets.TextField(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), editTag);
            y += 30f;

            // 重要性
            Widgets.Label(new Rect(innerRect.x, y, 100f, 25f), $"重要性: {editImportance:F2}");
            editImportance = Widgets.HorizontalSlider(new Rect(innerRect.x + 100f, y, innerRect.width - 100f, 25f), editImportance, 0f, 1f);
            y += 35f;

            // 关键词
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 25f), "关键词 (用逗号分隔，留空则自动提取):");
            y += 25f;
            Rect keywordsRect = new Rect(innerRect.x, y, innerRect.width, 25f);
            editKeywords = Widgets.TextField(keywordsRect, editKeywords);
            y += 30f;

            // 内容
            Widgets.Label(new Rect(innerRect.x, y, innerRect.width, 25f), "内容:");
            y += 25f;
            
            Rect contentRect = new Rect(innerRect.x, y, innerRect.width, innerRect.height - y - 50f);
            editContent = Widgets.TextArea(contentRect, editContent);

            // 按钮行
            Rect buttonRect = new Rect(innerRect.x, innerRect.yMax - 40f, 100f, 35f);
            
            if (Widgets.ButtonText(buttonRect, "保存"))
            {
                SaveEdit();
            }
            
            buttonRect.x += 110f;
            if (Widgets.ButtonText(buttonRect, "取消"))
            {
                editMode = false;
            }
        }

        private void CreateNewEntry()
        {
            editMode = true;
            editTag = "新标签";
            editContent = "";
            editImportance = 0.5f;
            editKeywords = ""; // 新建条目时清空关键词字段
            selectedEntry = null;
        }

        private void StartEdit(CommonKnowledgeEntry entry)
        {
            editMode = true;
            editTag = entry.tag;
            editContent = entry.content;
            editImportance = entry.importance;
            editKeywords = string.Join(", ", entry.keywords); // 编辑时填充关键词字段
        }

        private void SaveEdit()
        {
            if (string.IsNullOrEmpty(editContent))
            {
                Messages.Message("内容不能为空", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (selectedEntry != null)
            {
                // 编辑现有条目
                selectedEntry.tag = editTag;
                selectedEntry.content = editContent;
                selectedEntry.importance = editImportance;
                
                // 处理关键词
                if (string.IsNullOrWhiteSpace(editKeywords))
                {
                    // 自动提取
                    selectedEntry.ExtractKeywords();
                }
                else
                {
                    // 使用用户输入的关键词
                    selectedEntry.keywords = editKeywords
                        .Split(',')
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();
                }
            }
            else
            {
                // 创建新条目
                var newEntry = new CommonKnowledgeEntry(editTag, editContent);
                newEntry.importance = editImportance;
                
                // 处理关键词
                if (!string.IsNullOrWhiteSpace(editKeywords))
                {
                    newEntry.keywords = editKeywords
                        .Split(',')
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();
                }
                
                library.AddEntry(newEntry);
                selectedEntry = newEntry;
            }

            editMode = false;
            Messages.Message("保存成功", MessageTypeDefOf.PositiveEvent, false);
        }

        private void DeleteSelectedEntry()
        {
            if (selectedEntry == null)
                return;

            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "确定要删除这条常识吗？",
                delegate
                {
                    library.RemoveEntry(selectedEntry);
                    selectedEntry = null;
                    Messages.Message("已删除", MessageTypeDefOf.TaskCompletion, false);
                }
            ));
        }

        private void ClearAllEntries()
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                $"确定要清空所有 {library.Entries.Count} 条常识吗？此操作不可撤销！",
                delegate
                {
                    library.Clear();
                    selectedEntry = null;
                    Messages.Message("已清空常识库", MessageTypeDefOf.TaskCompletion, false);
                }
            ));
        }

        private void ShowImportDialog()
        {
            Dialog_TextInput dialog = new Dialog_TextInput(
                "导入常识",
                "请输入或粘贴常识文本\n格式: [标签]内容\n每行一条",
                "",
                delegate(string text)
                {
                    int count = library.ImportFromText(text, false);
                    Messages.Message($"成功导入 {count} 条常识", MessageTypeDefOf.PositiveEvent, false);
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
                    Messages.Message("没有可导出的内容", MessageTypeDefOf.RejectInput, false);
                    return;
                }

                // 保存到文件
                string fileName = $"CommonKnowledge_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string filePath = Path.Combine(GenFilePaths.SaveDataFolderPath, fileName);
                
                File.WriteAllText(filePath, exportText);
                
                Messages.Message($"已导出到: {filePath}", MessageTypeDefOf.PositiveEvent, false);
                Application.OpenURL(GenFilePaths.SaveDataFolderPath);
            }
            catch (Exception ex)
            {
                Log.Error($"导出常识库失败: {ex}");
                Messages.Message("导出失败: " + ex.Message, MessageTypeDefOf.RejectInput, false);
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

            // 文本输入区域
            Rect textRect = new Rect(0f, y, inRect.width, inRect.height - y - 50f);
            if (multiline)
            {
                text = Widgets.TextArea(textRect, text);
            }
            else
            {
                text = Widgets.TextField(textRect, text);
            }

            // 按钮
            Rect buttonRect = new Rect(inRect.width - 220f, inRect.height - 40f, 100f, 35f);
            if (Widgets.ButtonText(buttonRect, "确定"))
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
