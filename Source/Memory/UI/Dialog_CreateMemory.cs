using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// 手动创建记忆的对话框
    /// </summary>
    public class Dialog_CreateMemory : Window
    {
        private readonly Pawn pawn;
        private readonly FourLayerMemoryComp memoryComp;
        private readonly MemoryLayer targetLayer;
        private readonly MemoryType memoryType;
        
        private string contentText = "";
        private string notesText = "";
        private string tagsText = "";
        private float importance = 0.7f;
        private bool isPinned = false;
        
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(600f, 450f);

        public Dialog_CreateMemory(Pawn pawn, FourLayerMemoryComp memoryComp, MemoryLayer targetLayer, MemoryType memoryType)
        {
            this.pawn = pawn;
            this.memoryComp = memoryComp;
            this.targetLayer = targetLayer;
            this.memoryType = memoryType;
            
            this.doCloseButton = false; // ⭐ 禁用默认关闭按钮，使用自定义按钮
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.closeOnClickedOutside = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float yPos = 0f;
            
            // 标题
            Text.Font = GameFont.Medium;
            string layerName = GetLayerDisplayName(targetLayer);
            string typeName = GetTypeDisplayName(memoryType);
            Rect titleRect = new Rect(0f, yPos, inRect.width, 35f);
            Widgets.Label(titleRect, "RimTalk_Memory_AddToLayer".Translate(typeName, layerName, pawn.LabelShort));
            Text.Font = GameFont.Small;
            
            yPos += 40f;
            Widgets.DrawLineHorizontal(0f, yPos, inRect.width);
            yPos += 10f;

            // 记忆内容
            Rect contentLabelRect = new Rect(0f, yPos, inRect.width, 24f);
            Widgets.Label(contentLabelRect, "RimTalk_Memory_Content".Translate());
            yPos += 26f;
            
            Rect contentRect = new Rect(0f, yPos, inRect.width, 100f);
            contentText = GUI.TextArea(contentRect, contentText);
            yPos += 105f;

            // 标签（可选）
            Rect tagsLabelRect = new Rect(0f, yPos, inRect.width, 24f);
            Widgets.Label(tagsLabelRect, "RimTalk_Memory_TagsOptional".Translate());
            yPos += 26f;
            
            Rect tagsRect = new Rect(0f, yPos, inRect.width, 30f);
            tagsText = Widgets.TextField(tagsRect, tagsText);
            yPos += 35f;

            // 备注（可选）
            Rect notesLabelRect = new Rect(0f, yPos, inRect.width, 24f);
            Widgets.Label(notesLabelRect, "RimTalk_Memory_NotesOptional".Translate());
            yPos += 26f;
            
            Rect notesRect = new Rect(0f, yPos, inRect.width, 30f);
            notesText = Widgets.TextField(notesRect, notesText);
            yPos += 35f;

            // 重要性滑块
            Rect importanceLabelRect = new Rect(0f, yPos, inRect.width, 24f);
            Widgets.Label(importanceLabelRect, "RimTalk_Memory_ImportanceLabel".Translate(importance.ToString("F2")));
            yPos += 26f;
            
            Rect importanceRect = new Rect(0f, yPos, inRect.width, 25f);
            importance = Widgets.HorizontalSlider(importanceRect, importance, 0.1f, 1.0f, true);
            yPos += 30f;

            // 固定复选框
            Rect pinnedRect = new Rect(0f, yPos, inRect.width, 30f);
            Widgets.CheckboxLabeled(pinnedRect, "RimTalk_Memory_PinMemory".Translate(), ref isPinned);
            yPos += 35f;
            
            Widgets.DrawLineHorizontal(0f, yPos, inRect.width);
            yPos += 10f;

            // ⭐ 操作按钮 - 确保在底部可见
            float buttonWidth = (inRect.width - 10f) / 2f;
            float buttonY = inRect.height - 40f; // 固定在底部
            
            // 保存按钮
            Rect saveButtonRect = new Rect(0f, buttonY, buttonWidth, 35f);
            if (Widgets.ButtonText(saveButtonRect, "RimTalk_Knowledge_Save".Translate()))
            {
                if (string.IsNullOrWhiteSpace(contentText))
                {
                    Messages.Message("RimTalk_Memory_ContentRequired".Translate(), MessageTypeDefOf.RejectInput);
                }
                else
                {
                    SaveMemory();
                    Close();
                }
            }
            
            // 取消按钮
            Rect cancelButtonRect = new Rect(buttonWidth + 10f, buttonY, buttonWidth, 35f);
            if (Widgets.ButtonText(cancelButtonRect, "RimTalk_Knowledge_Cancel".Translate()))
            {
                Close();
            }
        }

        private void SaveMemory()
        {
            if (memoryComp == null) return;

            var newMemory = new MemoryEntry(
                content: contentText.Trim(),
                type: memoryType,
                layer: targetLayer,
                importance: importance
            );

            // 添加标签
            if (!string.IsNullOrWhiteSpace(tagsText))
            {
                string[] tags = tagsText.Split(',');
                foreach (var tag in tags)
                {
                    string trimmedTag = tag.Trim();
                    if (!string.IsNullOrEmpty(trimmedTag))
                    {
                        newMemory.AddTag(trimmedTag);
                    }
                }
            }

            // 添加备注
            if (!string.IsNullOrWhiteSpace(notesText))
            {
                newMemory.notes = notesText.Trim();
            }

            // 设置固定状态
            newMemory.isPinned = isPinned;

            // 添加"手动添加"标签
            newMemory.AddTag("手动添加");

            // 根据目标层级添加到相应的记忆列表
            switch (targetLayer)
            {
                case MemoryLayer.Active:
                    memoryComp.ActiveMemories.Insert(0, newMemory);
                    Messages.Message($"已添加到 {pawn.LabelShort} 的ABM", MessageTypeDefOf.TaskCompletion);
                    break;
                    
                case MemoryLayer.Situational:
                    memoryComp.SituationalMemories.Insert(0, newMemory);
                    Messages.Message($"已添加到 {pawn.LabelShort} 的SCM", MessageTypeDefOf.TaskCompletion);
                    break;
                    
                case MemoryLayer.EventLog:
                    memoryComp.EventLogMemories.Insert(0, newMemory);
                    Messages.Message("RimTalk_Memory_AddedToELS".Translate(pawn.LabelShort), MessageTypeDefOf.TaskCompletion);
                    break;
                    
                case MemoryLayer.Archive:
                    memoryComp.ArchiveMemories.Insert(0, newMemory);
                    Messages.Message("RimTalk_Memory_AddedToCLPA".Translate(pawn.LabelShort), MessageTypeDefOf.TaskCompletion);
                    break;
                    
                default:
                    Log.Warning($"[RimTalk Memory] 不支持手动添加到 {targetLayer} 层级");
                    break;
            }
        }

        private string GetLayerDisplayName(MemoryLayer layer)
        {
            switch (layer)
            {
                case MemoryLayer.Active:
                    return "ABM";
                case MemoryLayer.Situational:
                    return "SCM";
                case MemoryLayer.EventLog:
                    return "RimTalk_Layer_ELS".Translate();
                case MemoryLayer.Archive:
                    return "RimTalk_Layer_CLPA".Translate();
                default:
                    return layer.ToString();
            }
        }

        private string GetTypeDisplayName(MemoryType type)
        {
            switch (type)
            {
                case MemoryType.Action:
                    return "RimTalk_Type_Action".Translate();
                case MemoryType.Conversation:
                    return "RimTalk_Type_Conversation".Translate();
                default:
                    return type.ToString();
            }
        }
    }
}
