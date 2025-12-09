using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet; // ? 新增：用于检测商队和运输舱
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 自动生成Pawn状态常识（殖民者标识）
    /// 每24小时更新一次，不会覆盖用户手动修改
    /// 
    /// ? v3.3.2.32 重构版：
    /// - 修复休眠舱/远行/空投舱导致的重置Bug
    /// - 允许永久记录（移除7天限制）
    /// - 7天后改为"资深成员"描述
    /// - 覆盖所有地图+商队+运输舱中的小人
    /// </summary>
    public static class PawnStatusKnowledgeGenerator
    {
        // 记录每个Pawn上次更新时间
        private static Dictionary<int, int> lastUpdateTicks = new Dictionary<int, int>();
        private const int UPDATE_INTERVAL_TICKS = 60000; // 24小时 = 60000 ticks
        
        // ? NEW_COLONIST_THRESHOLD_DAYS 改为描述切换阈值（不再删除记录）
        private const int NEW_COLONIST_THRESHOLD_DAYS = 7;
        
        /// <summary>
        /// 更新所有殖民者的状态常识（每小时检查一次）
        /// 只更新距离上次更新>=24小时的Pawn
        /// ? v3.3.2.32: 覆盖所有地图+商队中的小人
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
            
            // ? v3.3.2.32: 遍历所有可能的位置获取殖民者
            var allColonists = new List<Pawn>();
            
            // 1. 所有地图上的殖民者
            foreach (var map in Find.Maps)
            {
                if (map.mapPawns != null)
                {
                    allColonists.AddRange(map.mapPawns.FreeColonists);
                }
            }
            
            // 2. 商队中的殖民者
            foreach (var caravan in Find.WorldObjects.Caravans)
            {
                if (caravan.IsPlayerControlled && caravan.pawns != null)
                {
                    foreach (var pawn in caravan.pawns.InnerListForReading)
                    {
                        if (pawn.IsColonist && !allColonists.Contains(pawn))
                        {
                            allColonists.Add(pawn);
                        }
                    }
                }
            }
            
            foreach (var pawn in allColonists)
            {
                try
                {
                    int pawnID = pawn.thingIDNumber;
                    
                    // 记录加入时间（如果尚未记录）
                    if (!colonistJoinTicks.ContainsKey(pawnID))
                    {
                        // 尝试从Pawn的records中获取真实的加入时间
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
                    if (Prefs.DevMode && UnityEngine.Random.value < 0.2f)
                    {
                        Log.Error($"[PawnStatus] Error updating status for {pawn.LabelShort}: {ex.Message}");
                    }
                }
            }
            
            if (updatedCount > 0 && Prefs.DevMode && UnityEngine.Random.value < 0.1f)
            {
                Log.Message($"[PawnStatus] Updated {updatedCount} colonist status knowledge entries");
            }
        }

        /// <summary>
        /// 为单个Pawn更新状态常识
        /// 不会覆盖用户手动修改（标记为"用户编辑"等）
        /// ? v3.3.2.32: 永久记录，7天后描述切换为"资深成员"
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
                
                // 计算殖民者加入当前时间 - 防止时间倒流
                int pawnID = pawn.thingIDNumber;
                
                if (!colonistJoinTicks.TryGetValue(pawnID, out int joinTick))
                {
                    // 理论上不应该发生（UpdateAllColonistStatus已经确保记录了）
                    joinTick = currentTick;
                    colonistJoinTicks[pawnID] = joinTick;
                    
                    if (Prefs.DevMode && UnityEngine.Random.value < 0.2f)
                    {
                        Log.Warning($"[PawnStatus] No join record for {pawn.LabelShort}, using current time");
                    }
                }
                
                int ticksInColony = currentTick - joinTick;
                int daysInColony = ticksInColony / GenDate.TicksPerDay;
                
                // 防止负数（如果加入时间错误）
                if (daysInColony < 0)
                {
                    Log.Error($"[PawnStatus] Negative days for {pawn.LabelShort}: {daysInColony}, resetting join time");
                    colonistJoinTicks[pawnID] = currentTick;
                    daysInColony = 0;
                }

                // 使用唯一标签
                string statusTag = $"殖民者状态,{pawn.LabelShort}";
                var existingEntry = library.Entries.FirstOrDefault(e => 
                    e.tag.Contains(pawn.LabelShort) && 
                    e.tag.Contains("殖民者状态")
                );

                // ? v3.3.2.32: 永久记录逻辑 - 不再删除，只更新描述
                string newContent = GenerateStatusContent(pawn, daysInColony, joinTick);
                float defaultImportance = 0.5f;

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
                        
                        if (Prefs.DevMode && UnityEngine.Random.value < 0.05f)
                        {
                            Log.Message($"[PawnStatus] Updated: {pawn.LabelShort} (days: {daysInColony}) -> {newContent}");
                        }
                    }
                    // 如果被用户手动修改，则不更新
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
                    
                    if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                    {
                        Log.Message($"[PawnStatus] Created: {pawn.LabelShort} (days: {daysInColony}, importance: {defaultImportance:F2})");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[PawnStatus] Failed to update status for {pawn?.LabelShort ?? "Unknown"}: {ex.Message}");
            }
        }

        /// <summary>
        /// 生成状态描述文本（优化为自然人称视角）
        /// 当Pawn首次被发现角色时，系统会自动注入其常识
        /// ? v3.3.2.32: 7天后改为"资深成员"描述，让AI知道他们是老手
        /// </summary>
        private static string GenerateStatusContent(Pawn pawn, int daysInColony, int joinTick)
        {
            string name = pawn.LabelShort;
            
            // 计算加入日期（游戏内日期）
            int joinDay = GenDate.DayOfYear(joinTick, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x);
            Quadrum joinQuadrum = GenDate.Quadrum(joinTick, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x);
            int joinYear = GenDate.Year(joinTick, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile).x);
            
            // 格式化日期（例如：X月, 5500年）
            string joinDate = $"{joinQuadrum.Label()} {joinDay}日, {joinYear}年";
            
            // 获取完整种族信息（种族+亚种）
            string raceInfo = GetCompleteRaceInfo(pawn);
            
            // ? v3.3.2.32: 根据天数生成不同描述
            string baseDescription = "";
            
            if (daysInColony < 7)
            {
                // < 7天：新成员描述
                if (daysInColony == 0)
                {
                    baseDescription = $"{name}是殖民地的新成员，今天({joinDate})刚加入";
                }
                else if (daysInColony == 1)
                {
                    baseDescription = $"{name}是殖民地的新成员，昨天({joinDate})加入";
                }
                else
                {
                    baseDescription = $"{name}是殖民地的新成员，{daysInColony}天前({joinDate})加入";
                }
            }
            else
            {
                // >= 7天：资深成员描述
                baseDescription = $"{name}是殖民地的资深成员，已加入殖民地 {daysInColony} 天（加入于{joinDate}），对殖民地的历史和成员关系较为熟悉";
            }
            
            // 附加种族信息和提示信息
            if (!string.IsNullOrEmpty(raceInfo))
            {
                if (daysInColony < 7)
                {
                    return $"{baseDescription}。{raceInfo}。对殖民地的历史和成员关系尚不熟悉";
                }
                else
                {
                    return $"{baseDescription}。{raceInfo}";
                }
            }
            else
            {
                return baseDescription;
            }
        }
        
        /// <summary>
        /// 获取完整种族信息（种族+亚种）
        /// 例如："人类-基准人"、"灵能者-宿主灵能者"、"龙王种-斯拉库鲁"
        /// </summary>
        private static string GetCompleteRaceInfo(Pawn pawn)
        {
            if (pawn?.def == null)
                return "";
            
            try
            {
                string pawnName = pawn.LabelShort;
                
                // 1. 获取主种族名称
                string raceName = pawn.def.label ?? pawn.def.defName;
                
                // 2. 尝试获取亚种信息（优先从基因获得）
                string xenotypeName = "";
                
                // 方法A：检查pawn.genes.Xenotype（标准Biotech DLC）
                if (pawn.genes != null && pawn.genes.Xenotype != null)
                {
                    xenotypeName = pawn.genes.Xenotype.label ?? pawn.genes.Xenotype.defName;
                }
                
                // 方法B：检查pawn.story.xenotype（旧版API）
                if (string.IsNullOrEmpty(xenotypeName) && pawn.story != null)
                {
                    var xenotypeField = pawn.story.GetType().GetField("xenotype");
                    if (xenotypeField != null)
                    {
                        var xenotype = xenotypeField.GetValue(pawn.story);
                        if (xenotype != null)
                        {
                            var labelProp = xenotype.GetType().GetProperty("label");
                            if (labelProp != null)
                            {
                                xenotypeName = labelProp.GetValue(xenotype) as string;
                            }
                        }
                    }
                }
                
                // 方法C：检查CustomXenotype（自定义名字）
                if (string.IsNullOrEmpty(xenotypeName) && pawn.genes != null)
                {
                    var customXenotypeField = pawn.genes.GetType().GetField("xenotypeName");
                    if (customXenotypeField != null)
                    {
                        xenotypeName = customXenotypeField.GetValue(pawn.genes) as string;
                    }
                }
                
                // 方法D：从def.description提取（适用于Mod添加的种族）
                if (string.IsNullOrEmpty(xenotypeName) && !string.IsNullOrEmpty(pawn.def.description))
                {
                    // 尝试从描述中提取关键词（例如"灵能者"、"斯拉"等）
                    var descWords = pawn.def.description.Split(new[] { ' ', '，', '。', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var word in descWords)
                    {
                        // 检测常见种族关键词
                        if (word.Length >= 2 && word.Length <= 6 && 
                            (word.Contains("灵能") || word.Contains("种") || word.Contains("型")))
                        {
                            xenotypeName = word;
                            break;
                        }
                    }
                }
                
                // 3. 组合种族和亚种描述
                if (!string.IsNullOrEmpty(xenotypeName))
                {
                    // 避免重复（如"人类-人类"）
                    if (xenotypeName.Equals(raceName, StringComparison.OrdinalIgnoreCase))
                    {
                        return $"{pawnName}的种族是{raceName}";
                    }
                    else
                    {
                        return $"{pawnName}的种族是{raceName}-{xenotypeName}";
                    }
                }
                else
                {
                    // 只有主种族
                    return $"{pawnName}的种族是{raceName}";
                }
            }
            catch (Exception ex)
            {
                // 容错：如果种族信息获取失败时，返回基础信息
                if (Prefs.DevMode)
                {
                    Log.Warning($"[PawnStatus] Failed to extract race info for {pawn.LabelShort}: {ex.Message}");
                }
                
                return $"{pawn.LabelShort}的种族是{pawn.def?.label ?? "未知"}";
            }
        }
        
        /// <summary>
        /// 获取Pawn真实的加入殖民地时间
        /// </summary>
        private static int GetPawnRealJoinTick(Pawn pawn, int fallbackTick)
        {
            try
            {
                // 方法1：从records.colonistSince获取（最准确）
                if (pawn.records != null)
                {
                    // colonistSince是殖民者记录中的游戏tick
                    // 但该字段可能不存在或为0（对于非殖民者）
                    var recordDef = DefDatabase<RecordDef>.GetNamed("TimeAsColonistOrColonyAnimal", false);
                    if (recordDef != null)
                    {
                        // 获取作为record的时间
                        // 加入时间 = 当前tick - 作为殖民者的时长
                        float timeAsColonist = pawn.records.GetValue(recordDef);
                        if (timeAsColonist > 0)
                        {
                            int joinTick = fallbackTick - (int)timeAsColonist;
                            
                            // 安全性检查：加入时间不能晚于游戏开始
                            if (joinTick >= 0 && joinTick <= fallbackTick)
                            {
                                return joinTick;
                            }
                        }
                    }
                }
                
                // 方法2：从spawned时间推断（不太准确，但聊胜于无）
                if (pawn.Spawned && pawn.Map != null)
                {
                    // 如果Pawn刚被生成（<1小时），可能刚刚加入
                    // 但这个逻辑不准确，所以使用fallback
                }
                
                // 方法3：后备 - 使用当前时间
                // 这意味着会被标记为新加入
                if (Prefs.DevMode)
                    Log.Warning($"[PawnStatus] Could not determine real join time for {pawn.LabelShort}, using current time as fallback");
                
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
                "刚加入", "新成员", "资深成员", "已加入殖民地" 
            };
            
            return autoKeywords.Any(k => content.Contains(k));
        }
        
        /// <summary>
        /// 清除已不存在的状态常识（Pawn离开或死亡）
        /// </summary>
        public static void CleanupPawnStatusKnowledge(Pawn pawn)
        {
            if (pawn == null) return;

            var library = MemoryManager.GetCommonKnowledge();
            if (library == null) return;

            var entry = library.Entries.FirstOrDefault(e => 
                e.tag.Contains(pawn.LabelShort) && 
                e.tag.Contains("殖民者状态")
            );
            
            if (entry != null)
            {
                library.RemoveEntry(entry);
                
                // 清除更新记录
                lastUpdateTicks.Remove(pawn.thingIDNumber);
                
                if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Message($"[PawnStatus] Removed status for {pawn.LabelShort}");
                }
            }
        }
        
        /// <summary>
        /// ? v3.3.2.32: 清理更新记录（修复重置Bug）
        /// 
        /// 关键修复：不要因为"找不到小人"就删除记录！
        /// 
        /// 只有当小人确实死亡 (pawn.Dead) 或不再属于玩家派系 (pawn.Faction != Faction.OfPlayer) 时，
        /// 才删除记录。这样可以完美解决休眠舱、远行、空投舱导致的重置问题。
        /// </summary>
        public static void CleanupUpdateRecords()
        {
            // ? 步骤1：收集所有存活的殖民者ID
            var allLivingColonists = new List<Pawn>();
            
            // 所有地图上的殖民者
            foreach (var map in Find.Maps)
            {
                if (map.mapPawns != null)
                {
                    allLivingColonists.AddRange(map.mapPawns.FreeColonists);
                }
            }
            
            // 商队中的殖民者
            foreach (var caravan in Find.WorldObjects.Caravans)
            {
                if (caravan.IsPlayerControlled && caravan.pawns != null)
                {
                    foreach (var pawn in caravan.pawns.InnerListForReading)
                    {
                        if (pawn.IsColonist && !allLivingColonists.Contains(pawn))
                        {
                            allLivingColonists.Add(pawn);
                        }
                    }
                }
            }
            
            var allColonistIDs = new HashSet<int>(allLivingColonists.Select(p => p.thingIDNumber));
            
            // ? 步骤2：检查所有 Pawn（包括死亡的）来确定哪些应该删除
            var memoryManager = Find.World?.GetComponent<MemoryManager>();
            if (memoryManager == null) return;
            
            var colonistJoinTicks = memoryManager.ColonistJoinTicks;
            
            // 遍历所有记录的 PawnID，检查它们是否还应该保留
            var toRemoveFromUpdate = new List<int>();
            var toRemoveFromJoin = new List<int>();
            
            foreach (var pawnID in colonistJoinTicks.Keys.ToList())
            {
                // 如果在存活列表中，跳过（保留记录）
                if (allColonistIDs.Contains(pawnID))
                    continue;
                
                // 尝试查找这个 Pawn（可能死亡、被捕获、或在其他地方）
                Pawn pawn = null;
                
                // 检查所有地图中的所有 Pawn（包括死亡的）
                foreach (var map in Find.Maps)
                {
                    pawn = map.mapPawns.AllPawns.FirstOrDefault(p => p.thingIDNumber == pawnID);
                    if (pawn != null) break;
                }
                
                // 检查世界 Pawns
                if (pawn == null && Find.WorldPawns != null)
                {
                    pawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == pawnID);
                }
                
                // ? 步骤3：决定是否删除记录
                bool shouldRemove = false;
                
                if (pawn == null)
                {
                    // 找不到 Pawn - 可能已经完全消失（极少见）
                    // 但不要急着删除！可能只是在特殊状态（如运输舱中）
                    // 保守策略：只删除记录超过30天的"幽灵"记录
                    int daysSinceJoin = (Find.TickManager.TicksGame - colonistJoinTicks[pawnID]) / GenDate.TicksPerDay;
                    if (daysSinceJoin > 30)
                    {
                        shouldRemove = true;
                        if (Prefs.DevMode)
                            Log.Message($"[PawnStatus] Removing ghost record (ID:{pawnID}, >30 days)");
                    }
                }
                else
                {
                    // 找到 Pawn - 检查是否真的应该删除
                    if (pawn.Dead)
                    {
                        shouldRemove = true;
                        if (Prefs.DevMode)
                            Log.Message($"[PawnStatus] Removing dead pawn: {pawn.LabelShort}");
                    }
                    else if (pawn.Faction != Faction.OfPlayer)
                    {
                        shouldRemove = true;
                        if (Prefs.DevMode)
                            Log.Message($"[PawnStatus] Removing non-player pawn: {pawn.LabelShort} (faction: {pawn.Faction?.Name ?? "none"})");
                    }
                    // 否则保留记录（即使在休眠舱、远行、空投舱中）
                }
                
                if (shouldRemove)
                {
                    toRemoveFromUpdate.Add(pawnID);
                    toRemoveFromJoin.Add(pawnID);
                }
            }
            
            // ? 步骤4：执行删除
            foreach (var id in toRemoveFromUpdate)
            {
                lastUpdateTicks.Remove(id);
            }
            
            foreach (var id in toRemoveFromJoin)
            {
                colonistJoinTicks.Remove(id);
            }
            
            // 日志输出
            if ((toRemoveFromUpdate.Count > 0 || toRemoveFromJoin.Count > 0) && Prefs.DevMode)
            {
                Log.Message($"[PawnStatus] Cleaned up {toRemoveFromUpdate.Count} update records, {toRemoveFromJoin.Count} join records");
            }
        }
    }
}
