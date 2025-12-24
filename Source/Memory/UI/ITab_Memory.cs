using UnityEngine;
using Verse;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// Inspector tab for viewing pawn memories
    /// </summary>
    public class ITab_Memory : ITab
    {
        // Virtualized list (instead of drawing all rows every frame)
        private VirtualListView<MemoryListEntry> virtualList;
        private readonly List<MemoryListEntry> cachedEntries = new List<MemoryListEntry>();

        private MemoryType? filterType = null;
        private bool showShortTerm = true; // 默认显示短期记忆
        private bool showLongTerm = true;  // 默认显示长期记忆

        public ITab_Memory()
        {
            size = new Vector2(500f, 600f);
            labelKey = "RimTalk_TabMemory";
            tutorTag = "RimTalkMemory";

            virtualList = new VirtualListView<MemoryListEntry>(
                getItemHeight: _ => 80f,
                drawItem: (r, entry, _) => DrawMemoryRow(r, entry.memory, entry.isShortTerm)
            )
            {
                ItemSpacing = 5f,
                EmptyLabel = "RimTalk_Memory_NoMemories".Translate()
            };
        }

        protected override void FillTab()
        {
            // Get selected pawn
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) return;

            var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
            if (memoryComp == null) return;

            Rect rect = new Rect(0f, 0f, size.x, size.y).ContractedBy(10f);
            GUI.BeginGroup(rect);

            // Header
            Rect headerRect = new Rect(0f, 0f, rect.width, 40f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, TranslatorFormattedStringExtensions.Translate("RimTalk_MemoryTitle", pawn.LabelShort));
            Text.Font = GameFont.Small;

            // Filter buttons
            Rect filterRect = new Rect(0f, 45f, rect.width, 30f);
            DrawFilterButtons(filterRect);

            // Memory type toggle buttons (短期/长期切换)
            Rect toggleRect = new Rect(0f, 80f, rect.width, 30f);
            DrawMemoryTypeToggles(toggleRect);

            // Memory stats
            Rect statsRect = new Rect(0f, 115f, rect.width, 40f);
            DrawMemoryStats(statsRect, memoryComp);

            // Memory list
            Rect listRect = new Rect(0f, 160f, rect.width, rect.height - 160f);
            DrawMemoryList(listRect, memoryComp);

            GUI.EndGroup();
        }

        private void DrawFilterButtons(Rect rect)
        {
            // 动态按钮数量：1个“全部”+枚举中的记忆类型按钮（排除未实现 Observation）
            var types = System.Enum.GetValues(typeof(MemoryType)).Cast<MemoryType>()
                .Where(t => t != MemoryType.Observation) // 过滤未实现类型
                .ToList();
            int totalButtons = types.Count + 1; // 包含“全部”
            float buttonWidth = rect.width / totalButtons;

            // 全部按钮
            Rect allRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
            if (Widgets.ButtonText(allRect, "RimTalk_Filter_All".Translate()))
            {
                filterType = null;
            }

            // 具体类型按钮
            for (int i = 0; i < types.Count; i++)
            {
                MemoryType type = types[i];
                Rect buttonRect = new Rect(rect.x + buttonWidth * (i + 1), rect.y, buttonWidth, rect.height);
                string buttonLabel = ("RimTalk_Filter_" + type.ToString()).Translate();
                if (Widgets.ButtonText(buttonRect, buttonLabel))
                {
                    filterType = type;
                }
            }
        }

        private void DrawMemoryTypeToggles(Rect rect)
        {
            float buttonWidth = rect.width / 2f;

            // 短期记忆按钮
            Rect shortTermRect = new Rect(rect.x, rect.y, buttonWidth - 2f, rect.height);
            string shortTermLabel = "RimTalk_ShortTermMemories".Translate() + (showShortTerm ? " ✓" : "");
            if (Widgets.ButtonText(shortTermRect, shortTermLabel))
            {
                bool newShowShortTerm = !showShortTerm;
                // 确保至少有一个被选中
                if (!newShowShortTerm && !showLongTerm)
                {
                    showLongTerm = true;
                }
                showShortTerm = newShowShortTerm;
            }

            // 长期记忆按钮
            Rect longTermRect = new Rect(rect.x + buttonWidth + 2f, rect.y, buttonWidth - 2f, rect.height);
            string longTermLabel = "RimTalk_LongTermMemories".Translate() + (showLongTerm ? " ✓" : "");
            if (Widgets.ButtonText(longTermRect, longTermLabel))
            {
                bool newShowLongTerm = !showLongTerm;
                // 确保至少有一个被选中
                if (!newShowLongTerm && !showShortTerm)
                {
                    showShortTerm = true;
                }
                showLongTerm = newShowLongTerm;
            }
        }

        private void DrawMemoryStats(Rect rect, PawnMemoryComp memoryComp)
        {
            Text.Anchor = TextAnchor.MiddleLeft;

            // 使用四层记忆架构的设置
            string stats = $"SCM: {memoryComp.SituationalMemories.Count}/{RimTalkMemoryPatchMod.Settings.maxSituationalMemories} | " +
                          $"ELS: {memoryComp.EventLogMemories.Count}/{RimTalkMemoryPatchMod.Settings.maxEventLogMemories}";

            Widgets.Label(rect, stats);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawMemoryList(Rect rect, PawnMemoryComp memoryComp)
        {
            cachedEntries.Clear();

            // 根据切换按钮决定显示哪些记忆
            if (showShortTerm)
            {
                foreach (var memory in memoryComp.ShortTermMemories)
                {
                    if (filterType == null || memory.type == filterType.Value)
                    {
                        cachedEntries.Add(new MemoryListEntry
                        {
                            memory = memory,
                            isShortTerm = true
                        });
                    }
                }
            }

            if (showLongTerm)
            {
                foreach (var memory in memoryComp.LongTermMemories)
                {
                    if (filterType == null || memory.type == filterType.Value)
                    {
                        cachedEntries.Add(new MemoryListEntry
                        {
                            memory = memory,
                            isShortTerm = false
                        });
                    }
                }
            }

            virtualList.EmptyLabel = "RimTalk_Memory_NoMemories".Translate();
            virtualList.SetItems(cachedEntries);
            virtualList.Draw(rect);
        }

        private void DrawMemoryRow(Rect rect, MemoryEntry memory, bool isShortTerm)
        {
            // Background
            Widgets.DrawBoxSolid(rect, new Color(0.2f, 0.2f, 0.2f, 0.5f));
            Widgets.DrawBox(rect);

            Rect innerRect = rect.ContractedBy(5f);

            // Type and time
            Rect headerRect = new Rect(innerRect.x, innerRect.y, innerRect.width, 20f);
            Text.Font = GameFont.Tiny;

            // Use translated memory type
            string memoryTypeLabel = ("RimTalk_MemoryType_" + memory.type.ToString()).Translate();

            string header = "[" + memoryTypeLabel + "] " + memory.TimeAgoString;
            if (!string.IsNullOrEmpty(memory.relatedPawnName))
                header += " - " + "RimTalk_With".Translate() + " " + memory.relatedPawnName;

            Widgets.Label(headerRect, header);

            // Content - 固定高度，截断显示，但添加 Tooltip
            Text.Font = GameFont.Small;

            // 截断到合适长度显示
            string displayText = memory.content;
            if (!string.IsNullOrEmpty(displayText) && displayText.Length > 80)
            {
                displayText = displayText.Substring(0, 77) + "...";
            }

            Rect contentRect = new Rect(innerRect.x, innerRect.y + 22f, innerRect.width, 40f);
            Widgets.Label(contentRect, displayText);

            // 添加 Tooltip 显示完整内容
            if (Mouse.IsOver(contentRect))
            {
                TooltipHandler.TipRegion(contentRect, memory.content);
            }

            // Importance bar
            Rect importanceRect = new Rect(innerRect.x, innerRect.y + 64f, innerRect.width, 8f);
            Widgets.FillableBar(importanceRect, Mathf.Clamp01(memory.importance),
                BaseContent.WhiteTex, null, false);

            Text.Font = GameFont.Small;
        }

        private class MemoryListEntry
        {
            public MemoryEntry memory;
            public bool isShortTerm;
        }
    }
}
