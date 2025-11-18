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

        public override Vector2 InitialSize => new Vector2(600f, 500f);

        public Dialog_CreateMemory(Pawn pawn, FourLayerMemoryComp memoryComp, MemoryLayer targetLayer, MemoryType memoryType)
        {
            this.pawn = pawn;
            this.memoryComp = memoryComp;
            this.targetLayer = targetLayer;
            this.memoryType = memoryType;
            
            this.doCloseButton = true;
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);

            // 标题
            Text.Font = GameFont.Medium;
            string layerName = GetLayerDisplayName(targetLayer);
            string typeName = GetTypeDisplayName(memoryType);
            listing.Label($"为 {pawn.LabelShort} 添加{typeName}到{layerName}");
            Text.Font = GameFont.Small;
            
            listing.Gap();
            listing.GapLine();
            listing.Gap();

            // 记忆内容
            Rect contentLabelRect = listing.GetRect(24f);
            Widgets.Label(contentLabelRect, "记忆内容：");
            
            Rect contentRect = listing.GetRect(120f);
            contentText = GUI.TextArea(contentRect, contentText);
            
            listing.Gap();

            // 标签（可选）
            listing.Label("标签（用逗号分隔，可选）：");
            tagsText = listing.TextEntry(tagsText);
            
            listing.Gap();

            // 备注（可选）
            listing.Label("备注（可选）：");
            notesText = listing.TextEntry(notesText);
            
            listing.Gap();

            // 重要性滑块
            listing.Label($"重要性：{importance:F2}");
            importance = listing.Slider(importance, 0.1f, 1.0f);
            
            listing.Gap();

            // 固定复选框
            listing.CheckboxLabeled("固定此记忆（不会被自动删除或衰减）", ref isPinned);
            
            listing.Gap();
            listing.GapLine();
            listing.Gap();

            // 操作按钮
            Rect buttonRect = listing.GetRect(35f);
            float buttonWidth = (buttonRect.width - 10f) / 2f;
            
            // 保存按钮
            if (Widgets.ButtonText(new Rect(buttonRect.x, buttonRect.y, buttonWidth, buttonRect.height), "保存"))
            {
                if (string.IsNullOrWhiteSpace(contentText))
                {
                    Messages.Message("记忆内容不能为空", MessageTypeDefOf.RejectInput);
                }
                else
                {
                    SaveMemory();
                    Close();
                }
            }
            
            // 取消按钮
            if (Widgets.ButtonText(new Rect(buttonRect.x + buttonWidth + 10f, buttonRect.y, buttonWidth, buttonRect.height), "取消"))
            {
                Close();
            }

            listing.End();
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
                case MemoryLayer.EventLog:
                    memoryComp.EventLogMemories.Insert(0, newMemory);
                    Messages.Message($"已将记忆添加到 {pawn.LabelShort} 的 ELS（中期记忆）", MessageTypeDefOf.TaskCompletion);
                    break;
                    
                case MemoryLayer.Archive:
                    memoryComp.ArchiveMemories.Insert(0, newMemory);
                    Messages.Message($"已将记忆添加到 {pawn.LabelShort} 的 CLPA（长期记忆）", MessageTypeDefOf.TaskCompletion);
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
                case MemoryLayer.EventLog:
                    return "ELS（中期记忆）";
                case MemoryLayer.Archive:
                    return "CLPA（长期记忆）";
                default:
                    return layer.ToString();
            }
        }

        private string GetTypeDisplayName(MemoryType type)
        {
            switch (type)
            {
                case MemoryType.Action:
                    return "行动记忆";
                case MemoryType.Conversation:
                    return "对话记忆";
                case MemoryType.Interaction:
                    return "互动记忆";
                default:
                    return type.ToString();
            }
        }
    }
}
