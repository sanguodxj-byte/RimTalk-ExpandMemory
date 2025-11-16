using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 四层记忆系统核心组件
    /// ABM -> SCM -> ELS -> CLPA
    /// </summary>
    public class FourLayerMemoryComp : ThingComp
    {
        // 四层记忆存储
        private List<MemoryEntry> activeMemories = new List<MemoryEntry>();      // ABM: 2-3条
        private List<MemoryEntry> situationalMemories = new List<MemoryEntry>(); // SCM: ~20条
        private List<MemoryEntry> eventLogMemories = new List<MemoryEntry>();    // ELS: ~50条
        private List<MemoryEntry> archiveMemories = new List<MemoryEntry>();     // CLPA: 无限制

        // 容量限制（从设置中读取）
        private int MAX_ACTIVE => RimTalkMemoryPatchMod.Settings.maxActiveMemories;
        private int MAX_SITUATIONAL => RimTalkMemoryPatchMod.Settings.maxSituationalMemories;
        private int MAX_EVENTLOG => RimTalkMemoryPatchMod.Settings.maxEventLogMemories;
        // CLPA 无限制

        public List<MemoryEntry> ActiveMemories => activeMemories;
        public List<MemoryEntry> SituationalMemories => situationalMemories;
        public List<MemoryEntry> EventLogMemories => eventLogMemories;
        public List<MemoryEntry> ArchiveMemories => archiveMemories;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref activeMemories, "activeMemories", LookMode.Deep);
            Scribe_Collections.Look(ref situationalMemories, "situationalMemories", LookMode.Deep);
            Scribe_Collections.Look(ref eventLogMemories, "eventLogMemories", LookMode.Deep);
            Scribe_Collections.Look(ref archiveMemories, "archiveMemories", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (activeMemories == null) activeMemories = new List<MemoryEntry>();
                if (situationalMemories == null) situationalMemories = new List<MemoryEntry>();
                if (eventLogMemories == null) eventLogMemories = new List<MemoryEntry>();
                if (archiveMemories == null) archiveMemories = new List<MemoryEntry>();
            }
        }

        /// <summary>
        /// 添加记忆到超短期记忆（ABM）
        /// </summary>
        public void AddActiveMemory(string content, MemoryType type, float importance = 1f, string relatedPawn = null)
        {
            var memory = new MemoryEntry(content, type, MemoryLayer.Active, importance, relatedPawn);
            
            // 自动提取关键词
            ExtractKeywords(memory);
            
            activeMemories.Insert(0, memory);
            
            // 只在调试模式输出详细日志（对话记录由上层统一输出）
            if (Prefs.DevMode)
            {
                Log.Message($"[FourLayer] {parent.LabelShort} ABM: {content.Substring(0, Math.Min(30, content.Length))}...");
            }

            // 超短期记忆满了，转移到短期
            if (activeMemories.Count > MAX_ACTIVE)
            {
                var oldest = activeMemories[activeMemories.Count - 1];
                activeMemories.RemoveAt(activeMemories.Count - 1);
                PromoteToSituational(oldest);
            }
        }

        /// <summary>
        /// 提升到短期记忆（SCM）
        /// </summary>
        private void PromoteToSituational(MemoryEntry memory)
        {
            memory.layer = MemoryLayer.Situational;
            situationalMemories.Insert(0, memory);
            
            // 只在调试模式输出详细日志
            if (Prefs.DevMode)
            {
                Log.Message($"[FourLayer] {parent.LabelShort} ABM -> SCM: {memory.content.Substring(0, Math.Min(30, memory.content.Length))}...");
            }

            // 短期记忆满了，触发每日总结（会在每天0点批量处理）
            if (situationalMemories.Count > MAX_SITUATIONAL * 1.5f)
            {
                Log.Warning($"[FourLayer] {parent.LabelShort} SCM overflow ({situationalMemories.Count}), waiting for daily summarization");
            }
        }

        /// <summary>
        /// 每日总结：SCM -> ELS (使用 AI)
        /// </summary>
        public void DailySummarization()
        {
            if (situationalMemories.Count == 0)
            {
                Log.Message($"[FourLayer] {parent.LabelShort}: No SCM to summarize");
                return;
            }

            Log.Message($"[FourLayer] 🌙 {parent.LabelShort}: Daily summarization START ({situationalMemories.Count} SCM)");

            var pawn = parent as Pawn;
            if (pawn == null)
            {
                Log.Error($"[FourLayer] {parent.LabelShort}: Parent is not a Pawn!");
                return;
            }

            // 按类型分组
            var byType = situationalMemories.GroupBy(m => m.type);
            int successCount = 0;
            int fallbackCount = 0;

            foreach (var typeGroup in byType)
            {
                var memories = typeGroup.ToList();
                Log.Message($"[FourLayer] {parent.LabelShort}: Processing {memories.Count} {typeGroup.Key} memories");
                
                // 使用 AI 总结
                string aiSummary = null;
                if (RimTalkMemoryPatchMod.Settings.useAISummarization)
                {
                    aiSummary = TryAISummarize(pawn, memories);
                    if (!string.IsNullOrEmpty(aiSummary))
                    {
                        Log.Message($"[FourLayer] {parent.LabelShort}: AI summary generated for {typeGroup.Key}");
                    }
                    else
                    {
                        Log.Warning($"[FourLayer] {parent.LabelShort}: AI summary failed for {typeGroup.Key}, using simple summary");
                    }
                }

                // 如果 AI 总结失败，使用简单总结
                string finalSummary = aiSummary;
                if (string.IsNullOrEmpty(finalSummary))
                {
                    finalSummary = CreateSimpleSummary(memories, typeGroup.Key);
                    fallbackCount++;
                }

                if (!string.IsNullOrEmpty(finalSummary))
                {
                    var summaryEntry = new MemoryEntry(
                        content: finalSummary,
                        type: typeGroup.Key,
                        layer: MemoryLayer.EventLog,
                        importance: memories.Average(m => m.importance) + 0.2f
                    );

                    // 继承关键词和标签
                    foreach (var m in memories)
                    {
                        summaryEntry.keywords.AddRange(m.keywords);
                        summaryEntry.tags.AddRange(m.tags);
                    }
                    summaryEntry.keywords = summaryEntry.keywords.Distinct().ToList();
                    summaryEntry.tags = summaryEntry.tags.Distinct().ToList();
                    
                    // 标记总结方式
                    if (string.IsNullOrEmpty(aiSummary))
                    {
                        summaryEntry.AddTag("简单总结");
                    }
                    else
                    {
                        summaryEntry.AddTag("AI总结");
                    }

                    eventLogMemories.Insert(0, summaryEntry);
                    successCount++;
                    Log.Message($"[FourLayer] ✅ SCM -> ELS: {finalSummary.Substring(0, Math.Min(50, finalSummary.Length))}...");
                }
                else
                {
                    Log.Error($"[FourLayer] {parent.LabelShort}: Failed to create any summary for {typeGroup.Key}");
                }
            }

            Log.Message($"[FourLayer] {parent.LabelShort}: Summarization complete - {successCount} summaries created ({fallbackCount} fallback)");

            // 清空 SCM
            situationalMemories.Clear();

            // 检查 ELS 容量
            TrimEventLog();
        }

        /// <summary>
        /// 创建简单总结（AI总结失败时的fallback）
        /// </summary>
        private string CreateSimpleSummary(List<MemoryEntry> memories, MemoryType type)
        {
            if (memories == null || memories.Count == 0)
                return null;

            var summary = new StringBuilder();
            
            // 对话类型：列出主要对话对象
            if (type == MemoryType.Conversation)
            {
                var byPerson = memories
                    .Where(m => !string.IsNullOrEmpty(m.relatedPawnName))
                    .GroupBy(m => m.relatedPawnName)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in byPerson.Take(5))
                {
                    if (shown > 0) summary.Append("；");
                    summary.Append($"与{group.Key}对话×{group.Count()}");
                    shown++;
                }
                
                if (shown == 0)
                {
                    summary.Append($"对话{memories.Count}次");
                }
            }
            // 行动类型：列出主要行动
            else if (type == MemoryType.Action)
            {
                // 提取行动关键词（动词）
                var actions = new List<string>();
                foreach (var m in memories)
                {
                    // 简单提取：取前10个字作为行动描述
                    string action = m.content.Length > 15 ? m.content.Substring(0, 15) : m.content;
                    actions.Add(action);
                }
                
                var grouped = actions
                    .GroupBy(a => a)
                    .OrderByDescending(g => g.Count());
                
                int shown = 0;
                foreach (var group in grouped.Take(3))
                {
                    if (shown > 0) summary.Append("；");
                    if (group.Count() > 1)
                    {
                        summary.Append($"{group.Key}×{group.Count()}");
                    }
                    else
                    {
                        summary.Append(group.Key);
                    }
                    shown++;
                }
            }
            // 其他类型：简单列举
            else
            {
                var grouped = memories
                    .GroupBy(m => m.content.Length > 20 ? m.content.Substring(0, 20) : m.content)
                    .OrderByDescending(g => g.Count());

                int shown = 0;
                foreach (var group in grouped.Take(5))
                {
                    if (shown > 0) summary.Append("；");
                    
                    string content = group.First().content;
                    // 不要截断太短
                    if (content.Length > 40)
                        content = content.Substring(0, 40) + "...";
                    
                    if (group.Count() > 1)
                    {
                        summary.Append($"{content}×{group.Count()}");
                    }
                    else
                    {
                        summary.Append(content);
                    }
                    shown++;
                }
            }

            // 添加总数
            if (summary.Length > 0 && memories.Count > 3)
            {
                summary.Append($"（共{memories.Count}条）");
            }

            return summary.Length > 0 ? summary.ToString() : $"{type}记忆{memories.Count}条";
        }

        /// <summary>
        /// 修剪中期记忆（ELS -> CLPA）
        /// </summary>
        private void TrimEventLog()
        {
            if (eventLogMemories.Count <= MAX_EVENTLOG)
                return;

            Log.Message($"[FourLayer] {parent.LabelShort}: ELS overflow ({eventLogMemories.Count}), archiving...");

            var pawn = parent as Pawn;
            if (pawn == null) return;

            // 将旧的 ELS 进一步总结到 CLPA
            var toArchive = eventLogMemories.Skip(MAX_EVENTLOG).ToList();
            
            // 按类型分组进行深度总结
            var byType = toArchive.GroupBy(m => m.type);
            
            foreach (var typeGroup in byType)
            {
                var memories = typeGroup.ToList();
                
                // 使用 AI 进行深度总结
                string archiveSummary = TryDeepArchive(pawn, memories);

                if (!string.IsNullOrEmpty(archiveSummary))
                {
                    var archiveEntry = new MemoryEntry(
                        content: archiveSummary,
                        type: typeGroup.Key,
                        layer: MemoryLayer.Archive,
                        importance: memories.Average(m => m.importance) + 0.3f
                    );

                    archiveEntry.AddTag("深度归档");
                    archiveEntry.AddTag($"源自{memories.Count}条ELS");

                    archiveMemories.Insert(0, archiveEntry);
                    Log.Message($"[FourLayer] ✅ ELS -> CLPA: {archiveSummary.Substring(0, Math.Min(50, archiveSummary.Length))}...");
                }
            }

            // 移除已归档的 ELS
            eventLogMemories.RemoveRange(MAX_EVENTLOG, eventLogMemories.Count - MAX_EVENTLOG);
        }

        /// <summary>
        /// 分层检索记忆（用于对话生成）
        /// </summary>
        public List<MemoryEntry> RetrieveMemories(MemoryQuery query)
        {
            var results = new List<MemoryEntry>();

            // 1. 首先检索 ABM（最高优先级）
            results.AddRange(activeMemories.Take(MAX_ACTIVE));

            // 2. 从 SCM 检索相关记忆
            var scmCandidates = situationalMemories
                .Where(m => MatchesQuery(m, query))
                .OrderByDescending(m => m.CalculateRetrievalScore(null, query.keywords))
                .Take(5);
            results.AddRange(scmCandidates);

            // 3. 如果需要更多上下文，检索 ELS
            if (query.includeContext && results.Count < query.maxCount)
            {
                var elsCandidates = eventLogMemories
                    .Where(m => MatchesQuery(m, query))
                    .OrderByDescending(m => m.CalculateRetrievalScore(null, query.keywords))
                    .Take(query.maxCount - results.Count);
                results.AddRange(elsCandidates);
            }

            // 4. 惰性加载 CLPA（只有在非常需要时）
            if (query.layer == MemoryLayer.Archive)
            {
                var clpaCandidates = archiveMemories
                    .Where(m => MatchesQuery(m, query))
                    .OrderByDescending(m => m.importance)
                    .Take(3);
                results.AddRange(clpaCandidates);
            }

            return results.Take(query.maxCount).ToList();
        }

        /// <summary>
        /// 检查记忆是否匹配查询
        /// </summary>
        private bool MatchesQuery(MemoryEntry memory, MemoryQuery query)
        {
            if (query.type.HasValue && memory.type != query.type.Value)
                return false;

            if (query.layer.HasValue && memory.layer != query.layer.Value)
                return false;

            if (!string.IsNullOrEmpty(query.relatedPawn) && memory.relatedPawnName != query.relatedPawn)
                return false;

            if (query.tags.Any() && !query.tags.Any(t => memory.tags.Contains(t)))
                return false;

            return true;
        }

        /// <summary>
        /// 活跃度衰减（每小时触发）
        /// </summary>
        public void DecayActivity()
        {
            foreach (var memory in situationalMemories)
            {
                memory.Decay(0.01f); // SCM 衰减快
            }

            foreach (var memory in eventLogMemories)
            {
                memory.Decay(0.005f); // ELS 衰减慢
            }

            foreach (var memory in archiveMemories)
            {
                memory.Decay(0.001f); // CLPA 衰减极慢
            }

            // 移除活跃度过低的记忆（但保留用户编辑和固定的）
            situationalMemories.RemoveAll(m => m.activity < 0.1f && !m.isPinned && !m.isUserEdited);
            eventLogMemories.RemoveAll(m => m.activity < 0.05f && !m.isPinned && !m.isUserEdited);
        }

        /// <summary>
        /// 提取关键词并添加相关标签（中文）
        /// </summary>
        private void ExtractKeywords(MemoryEntry memory)
        {
            if (string.IsNullOrEmpty(memory.content))
                return;

            var words = memory.content.Split(new[] { ' ', '，', '。', '、', '！', '？', '：', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            // 停用词
            string[] stopWords = { 
                "的", "了", "是", "在", "有", "和", "就", "不", "我", "你", "他", "她", "它",
                "这", "那", "个", "吗", "呢", "啊", "吧", "对", "说", "着", "把", "被", "给",
                "从", "到", "为", "以", "用", "要", "会", "能", "可以", "已经", "正在", "刚刚"
            };
            
            // 提取关键词
            foreach (var word in words)
            {
                string trimmed = word.Trim();
                if (trimmed.Length > 1 && !stopWords.Contains(trimmed))
                {
                    memory.AddKeyword(trimmed);
                }
            }

            // 智能添加标签
            AddSmartTags(memory);
        }

        /// <summary>
        /// 根据内容智能添加标签
        /// </summary>
        private void AddSmartTags(MemoryEntry memory)
        {
            string content = memory.content.ToLower();

            // 情绪标签
            if (content.Contains("开心") || content.Contains("高兴") || content.Contains("快乐") || content.Contains("愉快"))
                memory.AddTag(MemoryTags.开心);
            if (content.Contains("悲伤") || content.Contains("难过") || content.Contains("伤心") || content.Contains("哭泣"))
                memory.AddTag(MemoryTags.悲伤);
            if (content.Contains("愤怒") || content.Contains("生气") || content.Contains("暴怒") || content.Contains("发火"))
                memory.AddTag(MemoryTags.愤怒);
            if (content.Contains("焦虑") || content.Contains("担心") || content.Contains("紧张") || content.Contains("不安"))
                memory.AddTag(MemoryTags.焦虑);

            // 事件标签
            if (content.Contains("战斗") || content.Contains("打斗") || content.Contains("交战"))
                memory.AddTag(MemoryTags.战斗);
            if (content.Contains("袭击") || content.Contains("攻击") || content.Contains("raid") || content.Contains("attack"))
                memory.AddTag(MemoryTags.袭击);
            if (content.Contains("受伤") || content.Contains("伤害") || content.Contains("injured") || content.Contains("hurt"))
                memory.AddTag(MemoryTags.受伤);
            if (content.Contains("死亡") || content.Contains("去世") || content.Contains("died") || content.Contains("death"))
            {
                memory.AddTag(MemoryTags.死亡);
                memory.AddTag(MemoryTags.重要);
                memory.importance = UnityEngine.Mathf.Max(memory.importance, 0.9f);
            }
            if (content.Contains("完成") || content.Contains("任务") || content.Contains("finished") || content.Contains("completed"))
                memory.AddTag(MemoryTags.完成任务);

            // 社交标签
            if (content.Contains("闲聊") || content.Contains("chitchat") || content.Contains("small talk"))
                memory.AddTag(MemoryTags.闲聊);
            if (content.Contains("深谈") || content.Contains("deep talk") || content.Contains("deep conversation"))
                memory.AddTag(MemoryTags.深谈);
            if (content.Contains("争吵") || content.Contains("吵架") || content.Contains("quarrel") || content.Contains("fight"))
                memory.AddTag(MemoryTags.争吵);

            // 工作标签
            if (content.Contains("烹饪") || content.Contains("做饭") || content.Contains("cook"))
                memory.AddTag(MemoryTags.烹饪);
            if (content.Contains("建造") || content.Contains("建筑") || content.Contains("build") || content.Contains("construct"))
                memory.AddTag(MemoryTags.建造);
            if (content.Contains("种植") || content.Contains("植物") || content.Contains("plant") || content.Contains("grow"))
                memory.AddTag(MemoryTags.种植);
            if (content.Contains("采矿") || content.Contains("挖矿") || content.Contains("mine") || content.Contains("mining"))
                memory.AddTag(MemoryTags.采矿);
            if (content.Contains("研究") || content.Contains("科研") || content.Contains("research"))
                memory.AddTag(MemoryTags.研究);
            if (content.Contains("医疗") || content.Contains("治疗") || content.Contains("医治") || content.Contains("medical") || content.Contains("heal"))
                memory.AddTag(MemoryTags.医疗);

            // 特殊标记
            if (memory.isUserEdited)
                memory.AddTag(MemoryTags.用户编辑);
            if (memory.importance > 0.8f)
                memory.AddTag(MemoryTags.重要);
        }

        /// <summary>
        /// 尝试使用 AI 总结记忆
        /// </summary>
        private string TryAISummarize(Pawn pawn, List<MemoryEntry> memories)
        {
            // 使用独立的AI总结服务（不依赖RimTalk）
            return RimTalk.Memory.AI.IndependentAISummarizer.SummarizeMemories(pawn, memories, "standard");
        }

        /// <summary>
        /// 深度归档总结（ELS -> CLPA）
        /// </summary>
        private string TryDeepArchive(Pawn pawn, List<MemoryEntry> memories)
        {
            // 使用独立的AI总结服务
            return RimTalk.Memory.AI.IndependentAISummarizer.SummarizeMemories(pawn, memories, "deep_archive");
        }

        /// <summary>
        /// 编辑记忆（用户操作）
        /// </summary>
        public void EditMemory(string memoryId, string newContent, string notes = null)
        {
            var memory = FindMemoryById(memoryId);
            if (memory != null)
            {
                memory.content = newContent;
                memory.isUserEdited = true;
                if (!string.IsNullOrEmpty(notes))
                {
                    memory.notes = notes;
                }
                Log.Message($"[FourLayer] Memory edited: {memoryId}");
            }
        }

        /// <summary>
        /// 固定记忆（用户操作）
        /// </summary>
        public void PinMemory(string memoryId, bool pinned)
        {
            var memory = FindMemoryById(memoryId);
            if (memory != null)
            {
                memory.isPinned = pinned;
                Log.Message($"[FourLayer] Memory {(pinned ? "pinned" : "unpinned")}: {memoryId}");
            }
        }

        /// <summary>
        /// 删除记忆（用户操作）
        /// </summary>
        public void DeleteMemory(string memoryId)
        {
            activeMemories.RemoveAll(m => m.id == memoryId);
            situationalMemories.RemoveAll(m => m.id == memoryId);
            eventLogMemories.RemoveAll(m => m.id == memoryId);
            archiveMemories.RemoveAll(m => m.id == memoryId);
            Log.Message($"[FourLayer] Memory deleted: {memoryId}");
        }

        /// <summary>
        /// 根据ID查找记忆
        /// </summary>
        private MemoryEntry FindMemoryById(string id)
        {
            return activeMemories.FirstOrDefault(m => m.id == id)
                ?? situationalMemories.FirstOrDefault(m => m.id == id)
                ?? eventLogMemories.FirstOrDefault(m => m.id == id)
                ?? archiveMemories.FirstOrDefault(m => m.id == id);
        }

        /// <summary>
        /// 获取所有记忆（用于UI显示）
        /// </summary>
        public List<MemoryEntry> GetAllMemories()
        {
            var all = new List<MemoryEntry>();
            all.AddRange(activeMemories);
            all.AddRange(situationalMemories);
            all.AddRange(eventLogMemories);
            all.AddRange(archiveMemories);
            return all;
        }

        /// <summary>
        /// 清除对话上下文（ABM）
        /// </summary>
        public void ClearActiveMemory()
        {
            foreach (var memory in activeMemories.ToList())
            {
                PromoteToSituational(memory);
            }
            activeMemories.Clear();
            Log.Message($"[FourLayer] {parent.LabelShort}: ABM cleared");
        }
    }
}
