using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 自动生成Pawn状态常识（新殖民者标识）
    /// 每24小时更新一次，不覆盖用户手动修改
    /// 
    /// v2.4.5 简化版：
    /// - 删除时间线记录功能
    /// - 只标记"新人"，7天后自动删除
    /// - 使用7天作为新人判定阈值
    /// </summary>
    public static class PawnStatusKnowledgeGenerator
    {
        // 记录每个Pawn上次更新时间
        private static Dictionary<int, int> lastUpdateTicks = new Dictionary<int, int>();
        private const int UPDATE_INTERVAL_TICKS = 60000; // 24小时 = 60000 ticks
        
        // 新人判定阈值：7天
        private const int NEW_COLONIST_THRESHOLD_DAYS = 7;
        
        /// <summary>
        /// 更新所有殖民者的状态常识（每小时调用一次）
        /// 只更新距离上次更新>=24小时的Pawn
        /// </summary>
        public static void UpdateAllColonistStatus()
        {
            if (!RimTalkMemoryPatchMod.Settings.enablePawnStatusKnowledge)
                return;
            
            var library = MemoryManager.GetCommonKnowledge();
            if (library == null) return;

            int currentTick = Find.TickManager.TicksGame;
            int updatedCount = 0;
            
            // 获取MemoryManager（用于访问colonistJoinTicks字典）
            var memoryManager = Find.World?.GetComponent<MemoryManager>();
            if (memoryManager == null) return;
            
            var colonistJoinTicks = memoryManager.ColonistJoinTicks;
            
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    try
                    {
                        int pawnID = pawn.thingIDNumber;
                        
                        // ? 修复：使用Pawn真实的加入时间，而不是首次检测时间
                        if (!colonistJoinTicks.ContainsKey(pawnID))
                        {
                            // 尝试从Pawn的records中获取真实加入时间
                            int realJoinTick = GetPawnRealJoinTick(pawn, currentTick);
                            colonistJoinTicks[pawnID] = realJoinTick;
                            
                            if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                            {
                                int daysAgo = (currentTick - realJoinTick) / GenDate.TicksPerDay;
                                Log.Message($"[PawnStatus] First detection: {pawn.LabelShort}, joined {daysAgo} days ago (tick: {realJoinTick})");
                            }
                        }
                        
                        // 检查是否需要更新（24小时间隔）
                        if (!lastUpdateTicks.TryGetValue(pawnID, out int lastUpdate))
                        {
                            lastUpdate = 0; // 首次更新
                        }
                        
                        int ticksSinceUpdate = currentTick - lastUpdate;
                        
                        if (ticksSinceUpdate >= UPDATE_INTERVAL_TICKS)
                        {
                            UpdatePawnStatusKnowledge(pawn, library, currentTick, colonistJoinTicks);
                            lastUpdateTicks[pawnID] = currentTick;
                            updatedCount++;
                        }
                    }
                    catch (Exception ex)
                    {
                        // ? v3.3.2: 错误日志保留但降低频率
                        if (Prefs.DevMode && UnityEngine.Random.value < 0.2f)
                        {
                            Log.Error($"[PawnStatus] Error updating status for {pawn.LabelShort}: {ex.Message}");
                        }
                    }
                }
            }
            
            // ? v3.3.2: 降低日志输出 - 仅DevMode且10%概率
            if (updatedCount > 0 && Prefs.DevMode && UnityEngine.Random.value < 0.1f)
            {
                Log.Message($"[PawnStatus] Updated {updatedCount} colonist status knowledge entries");
            }
        }

        /// <summary>
        /// 为单个Pawn生成状态常识
        /// 保护用户的手动修改（标记为"用户编辑"等）
        /// </summary>
        public static void UpdatePawnStatusKnowledge(Pawn pawn, CommonKnowledgeLibrary library, int currentTick, Dictionary<int, int> colonistJoinTicks)
        {
            if (pawn == null || library == null || colonistJoinTicks == null) return;

            try
            {
                // 婴儿阶段（<3岁）不生成状态
                if (pawn.RaceProps != null && pawn.RaceProps.Humanlike)
                {
                    float ageYears = pawn.ageTracker.AgeBiologicalYearsFloat;
                    if (ageYears < 3f)
                    {
                        CleanupPawnStatusKnowledge(pawn);
                        return;
                    }
                }
                
                // 计算入殖天数：当前时间 - 加入时间
                int pawnID = pawn.thingIDNumber;
                
                if (!colonistJoinTicks.TryGetValue(pawnID, out int joinTick))
                {
                    // 理论上不应该发生（UpdateAllColonistStatus已经确保记录）
                    joinTick = currentTick;
                    colonistJoinTicks[pawnID] = joinTick;
                    
                    // ? v3.3.2: 降低警告输出
                    if (Prefs.DevMode && UnityEngine.Random.value < 0.2f)
                    {
                        Log.Warning($"[PawnStatus] No join record for {pawn.LabelShort}, using current time");
                    }
                }
                
                int ticksInColony = currentTick - joinTick;
                int daysInColony = ticksInColony / GenDate.TicksPerDay;
                
                // 防止负数（数据损坏情况）
                if (daysInColony < 0)
                {
                    // ? v3.3.2: 错误日志保留
                    Log.Error($"[PawnStatus] Negative days for {pawn.LabelShort}: {daysInColony}, resetting join time");
                    colonistJoinTicks[pawnID] = currentTick;
                    daysInColony = 0;
                }

                // 使用唯一标签
                string statusTag = $"新殖民者,{pawn.LabelShort}";
                var existingEntry = library.Entries.FirstOrDefault(e => 
                    e.tag.Contains(pawn.LabelShort) && 
                    e.tag.Contains("新殖民者")
                );

                // ? 核心逻辑：超过7天后删除常识
                if (daysInColony >= NEW_COLONIST_THRESHOLD_DAYS)
                {
                    if (existingEntry != null && !existingEntry.isUserEdited)
                    {
                        library.RemoveEntry(existingEntry);
                        
                        // ? v3.3.2: 降低日志输出
                        if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                        {
                            Log.Message($"[PawnStatus] Removed new colonist tag for {pawn.LabelShort} (>= {NEW_COLONIST_THRESHOLD_DAYS} days)");
                        }
                    }
                    return; // 7天后不再生成任何常识
                }

                // 未满7天：生成或更新"新成员"常识
                string newContent = GenerateStatusContent(pawn, daysInColony, joinTick);
                float defaultImportance = 0.5f; // ? v2.4.7: 降低重要性到0.5，避免AI过度强调新人状态

                if (existingEntry != null)
                {
                    // 检查是否为自动生成的内容（没有被用户编辑）
                    bool isAutoGenerated = !existingEntry.isUserEdited && 
                                          IsAutoGeneratedContent(existingEntry.content);
                    
                    if (isAutoGenerated)
                    {
                        // 只更新自动生成的内容
                        existingEntry.content = newContent;
                        existingEntry.importance = defaultImportance;
                        existingEntry.targetPawnId = pawn.thingIDNumber;
                        
                        // ? v3.3.2: 降低日志输出
                        if (Prefs.DevMode && UnityEngine.Random.value < 0.05f)
                        {
                            Log.Message($"[PawnStatus] Updated: {pawn.LabelShort} (days: {daysInColony}) -> {newContent}");
                        }
                    }
                    else
                    {
                        // ? v3.3.2: 降低日志输出
                        // 保护用户的手动修改
                    }
                }
                else
                {
                    // 创建新常识
                    var newEntry = new CommonKnowledgeEntry(statusTag, newContent)
                    {
                        importance = defaultImportance,
                        isEnabled = true,
                        isUserEdited = false,
                        targetPawnId = pawn.thingIDNumber
                    };
                    
                    library.AddEntry(newEntry);
                    
                    // ? v3.3.2: 降低日志输出
                    if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                    {
                        Log.Message($"[PawnStatus] Created: {pawn.LabelShort} (days: {daysInColony}, importance: {defaultImportance:F2})");
                    }
                }
            }
            catch (Exception ex)
            {
                // ? v3.3.2: 错误日志保留
                Log.Error($"[PawnStatus] Failed to update status for {pawn?.LabelShort ?? "Unknown"}: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成状态描述文本（优化为第三人称视角）
        /// 其他Pawn看到这条常识时，会知道对方是新人
        /// ? v2.4.6: 增加加入时间显示
        /// </summary>
        private static string GenerateStatusContent(Pawn pawn, int daysInColony, int joinTick)
        {
            string name = pawn.LabelShort;
            
            // 计算加入日期（游戏内日期）
            int joinDay = GenDate.DayOfYear(joinTick, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x);
            Quadrum joinQuadrum = GenDate.Quadrum(joinTick, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x);
            int joinYear = GenDate.Year(joinTick, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x);
            
            // 格式化日期：季节 X日, 5500年
            string joinDate = $"{joinQuadrum.Label()} {joinDay}日, {joinYear}年";
            
            // 使用第三人称客观描述，适合全局常识库
            // 任何Pawn看到这条信息时都能正确理解
            if (daysInColony == 0)
            {
                return $"{name}是殖民地的新成员，今天({joinDate})刚加入，对殖民地的历史和成员关系还不熟悉";
            }
            else if (daysInColony == 1)
            {
                return $"{name}是殖民地的新成员，昨天({joinDate})加入，对殖民地的历史和成员关系还不熟悉";
            }
            else
            {
                return $"{name}是殖民地的新成员，{daysInColony}天前({joinDate})加入，对殖民地的历史和成员关系还不熟悉";
            }
        }

        /// <summary>
        /// ? 获取Pawn真实的加入殖民地时间
        /// </summary>
        private static int GetPawnRealJoinTick(Pawn pawn, int fallbackTick)
        {
            try
            {
                // 方法1：从records.colonistSince获取（最准确）
                if (pawn.records != null)
                {
                    // colonistSince是殖民者加入的游戏tick
                    // 但这个字段可能不存在或为0（非殖民者）
                    var recordDef = DefDatabase<RecordDef>.GetNamed("TimeAsColonistOrColonyAnimal", false);
                    if (recordDef != null)
                    {
                        // 如果有这个record，说明是殖民者
                        // 计算加入时间 = 当前tick - 作为殖民者的时长
                        float timeAsColonist = pawn.records.GetValue(recordDef);
                        if (timeAsColonist > 0)
                        {
                            int joinTick = fallbackTick - (int)timeAsColonist;
                            
                            // 合理性检查：加入时间不能早于游戏开始
                            if (joinTick >= 0 && joinTick <= fallbackTick)
                            {
                                return joinTick;
                            }
                        }
                    }
                }
                
                // 方法2：从spawned时间推断（不太准确，但总比用当前时间好）
                if (pawn.Spawned && pawn.Map != null)
                {
                    // 如果Pawn刚生成不久（<1小时），可能是刚加入的
                    // 否则使用fallback
                }
                
                // 方法3：回退 - 使用当前时间
                // 但至少在日志中标记为不准确
                if (Prefs.DevMode)
                {
                    Log.Warning($"[PawnStatus] Could not determine real join time for {pawn.LabelShort}, using current time as fallback");
                }
                
                return fallbackTick;
            }
            catch (Exception ex)
            {
                Log.Error($"[PawnStatus] Error getting real join tick for {pawn?.LabelShort}: {ex.Message}");
                return fallbackTick;
            }
        }
        
        /// <summary>
        /// 检查内容是否为自动生成的（没有被用户编辑）
        /// </summary>
        private static bool IsAutoGeneratedContent(string content)
        {
            if (string.IsNullOrEmpty(content))
                return false;
            
            // 检查是否包含自动生成的关键词
            var autoKeywords = new[] 
            { 
                "刚加入", "新成员"
            };
            
            return autoKeywords.Any(k => content.Contains(k));
        }
        
        /// <summary>
        /// 清理已不存在的状态常识（Pawn离开或死亡）
        /// </summary>
        public static void CleanupPawnStatusKnowledge(Pawn pawn)
        {
            if (pawn == null) return;

            var library = MemoryManager.GetCommonKnowledge();
            if (library == null) return;

            var entry = library.Entries.FirstOrDefault(e => 
                e.tag.Contains(pawn.LabelShort) && 
                e.tag.Contains("新殖民者")
            );
            
            if (entry != null)
            {
                library.RemoveEntry(entry);
                
                // 清理更新记录
                lastUpdateTicks.Remove(pawn.thingIDNumber);
                
                // ? v3.3.2: 降低日志输出
                if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Message($"[PawnStatus] Removed status for {pawn.LabelShort}");
                }
            }
        }
        
        /// <summary>
        /// 清理更新记录（定期保养/清理）
        /// </summary>
        public static void CleanupUpdateRecords()
        {
            // 移除不存在的Pawn记录
            var allColonistIDs = new HashSet<int>();
            
            foreach (var map in Find.Maps)
            {
                foreach (var pawn in map.mapPawns.FreeColonists)
                {
                    allColonistIDs.Add(pawn.thingIDNumber);
                }
            }
            
            // 清理lastUpdateTicks（本地缓存）
            var toRemove = lastUpdateTicks.Keys.Where(id => !allColonistIDs.Contains(id)).ToList();
            foreach (var id in toRemove)
            {
                lastUpdateTicks.Remove(id);
            }
            
            // 清理colonistJoinTicks（持久化数据）
            var memoryManager = Find.World?.GetComponent<MemoryManager>();
            if (memoryManager != null)
            {
                var colonistJoinTicks = memoryManager.ColonistJoinTicks;
                var toRemoveJoin = colonistJoinTicks.Keys.Where(id => !allColonistIDs.Contains(id)).ToList();
                
                foreach (var id in toRemoveJoin)
                {
                    colonistJoinTicks.Remove(id);
                }
                
                // ? v3.3.2: 降低日志输出
                if ((toRemove.Count > 0 || toRemoveJoin.Count > 0) && Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Message($"[PawnStatus] Cleaned up {toRemove.Count} update records, {toRemoveJoin.Count} join records");
                }
            }
        }
    }
}
