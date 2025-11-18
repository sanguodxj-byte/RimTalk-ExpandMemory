using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// 记忆编辑对话框 - 四层记忆系统
    /// </summary>
    public class Dialog_EditMemory : Window
    {
        private MemoryEntry memory;
        private FourLayerMemoryComp memoryComp;
        private string editedContent;
        private string editedNotes;
        private Vector2 scrollPosition;
        private List<string> availableTags;
        private string newTagInput = "";

        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public Dialog_EditMemory(MemoryEntry memory, FourLayerMemoryComp comp)
        {
            this.memory = memory;
            this.memoryComp = comp;
            this.editedContent = memory.content;
            this.editedNotes = memory.notes ?? "";
            
            // 初始化可用标签列表
            availableTags = new List<string>
            {
                MemoryTags.开心, MemoryTags.悲伤, MemoryTags.愤怒, MemoryTags.焦虑, MemoryTags.平静,
                MemoryTags.战斗, MemoryTags.袭击, MemoryTags.受伤, MemoryTags.死亡, MemoryTags.完成任务,
                MemoryTags.闲聊, MemoryTags.深谈, MemoryTags.争吵, MemoryTags.友好, MemoryTags.敌对,
                MemoryTags.烹饪, MemoryTags.建造, MemoryTags.种植, MemoryTags.采矿, MemoryTags.研究, MemoryTags.医疗,
                MemoryTags.重要, MemoryTags.紧急
            };

            doCloseButton = false;
            doCloseX = true;
            forcePause = true;
            absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "编辑记忆");
            Text.Font = GameFont.Small;

            float curY = 45f;

            // 记忆信息
            Rect infoRect = new Rect(0f, curY, inRect.width, 60f);
            GUI.color = Color.gray;
            Widgets.Label(new Rect(infoRect.x, infoRect.y, infoRect.width, 25f), 
                $"类型: {memory.TypeName}  |  层级: {memory.LayerName}  |  时间: {memory.TimeAgoString}");
            Widgets.Label(new Rect(infoRect.x, infoRect.y + 25f, infoRect.width, 25f), 
                $"重要性: {memory.importance:F2}  |  活跃度: {memory.activity:F2}");
            GUI.color = Color.white;
            curY += 65f;

            // 内容编辑
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), "内容:");
            curY += 25f;
            
            Rect contentRect = new Rect(0f, curY, inRect.width, 80f);
            // 使用 TextArea 代替 TextAreaScrollable（RimWorld 1.5+）
            editedContent = Widgets.TextArea(contentRect, editedContent);
            curY += 85f;

            // 备注编辑
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), "备注:");
            curY += 25f;
            
            Rect notesRect = new Rect(0f, curY, inRect.width, 60f);
            editedNotes = Widgets.TextArea(notesRect, editedNotes);
            curY += 65f;

            // 标签管理
            Widgets.Label(new Rect(0f, curY, inRect.width, 25f), "标签:");
            curY += 25f;

            DrawTagsSection(new Rect(0f, curY, inRect.width, 100f));
            curY += 105f;

            // 固定选项
            Rect pinnedRect = new Rect(0f, curY, inRect.width, 30f);
            bool wasPinned = memory.isPinned;
            Widgets.CheckboxLabeled(pinnedRect, "固定此记忆（不会被删除或衰减）", ref memory.isPinned);
            if (memory.isPinned != wasPinned)
            {
                if (memory.isPinned)
                {
                    memory.AddTag(MemoryTags.重要);
                }
            }
            curY += 35f;

            // 底部按钮
            float buttonWidth = 120f;
            float buttonY = inRect.height - 40f;
            
            // 保存按钮
            if (Widgets.ButtonText(new Rect(inRect.width - buttonWidth * 2 - 10f, buttonY, buttonWidth, 35f), "保存"))
            {
                SaveChanges();
                Close();
            }

            // 取消按钮
            if (Widgets.ButtonText(new Rect(inRect.width - buttonWidth, buttonY, buttonWidth, 35f), "取消"))
            {
                Close();
            }
        }

        private void DrawTagsSection(Rect rect)
        {
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, availableTags.Count * 25f + 40f);
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);

            float curY = 0f;

            // 当前标签（可移除）
            if (memory.tags != null && memory.tags.Any())
            {
                foreach (var tag in memory.tags.ToList())
                {
                    Rect tagRect = new Rect(0f, curY, viewRect.width, 22f);
                    
                    // 标签名称
                    Rect labelRect = new Rect(tagRect.x + 5f, tagRect.y, tagRect.width - 70f, tagRect.height);
                    Widgets.Label(labelRect, $"✓ {tag}");
                    
                    // 移除按钮
                    Rect removeRect = new Rect(tagRect.xMax - 60f, tagRect.y, 55f, 22f);
                    if (Widgets.ButtonText(removeRect, "移除"))
                    {
                        memory.RemoveTag(tag);
                    }
                    
                    curY += 25f;
                }
                
                curY += 5f;
                Widgets.DrawLineHorizontal(0f, curY, viewRect.width);
                curY += 10f;
            }

            // 可用标签（可添加）
            foreach (var tag in availableTags)
            {
                if (memory.tags != null && memory.tags.Contains(tag))
                    continue;

                Rect tagRect = new Rect(0f, curY, viewRect.width, 22f);
                
                // 标签名称
                Rect labelRect = new Rect(tagRect.x + 5f, tagRect.y, tagRect.width - 70f, tagRect.height);
                GUI.color = Color.gray;
                Widgets.Label(labelRect, tag);
                GUI.color = Color.white;
                
                // 添加按钮
                Rect addRect = new Rect(tagRect.xMax - 60f, tagRect.y, 55f, 22f);
                if (Widgets.ButtonText(addRect, "添加"))
                {
                    memory.AddTag(tag);
                }
                
                curY += 25f;
            }

            // 自定义标签输入
            curY += 5f;
            Widgets.DrawLineHorizontal(0f, curY, viewRect.width);
            curY += 10f;
            
            Rect customLabelRect = new Rect(0f, curY, viewRect.width, 22f);
            Widgets.Label(customLabelRect, "自定义标签:");
            curY += 25f;
            
            Rect inputRect = new Rect(0f, curY, viewRect.width - 70f, 22f);
            newTagInput = Widgets.TextField(inputRect, newTagInput);
            
            Rect addCustomRect = new Rect(viewRect.width - 60f, curY, 55f, 22f);
            if (Widgets.ButtonText(addCustomRect, "添加") && !string.IsNullOrWhiteSpace(newTagInput))
            {
                memory.AddTag(newTagInput.Trim());
                newTagInput = "";
            }

            Widgets.EndScrollView();
        }

        private void SaveChanges()
        {
            memoryComp.EditMemory(memory.id, editedContent, editedNotes);
            
            // 添加用户编辑标签
            if (!memory.tags.Contains(MemoryTags.用户编辑))
            {
                memory.AddTag(MemoryTags.用户编辑);
            }

            Messages.Message($"记忆已更新", MessageTypeDefOf.TaskCompletion);
        }
    }
}
