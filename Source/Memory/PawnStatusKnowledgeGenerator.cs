using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 自动生成Pawn状态常识（殖民者标识）
    /// 每24小时更新一次，不会覆盖用户手动修改
    /// 
    /// ? v3.3.17: 重构版 - 移除缓存，直接使用RimWorld原生记录
    /// - 修复"今天加入"bug
    /// - 完全依赖pawn.records.TimeAsColonistOrColonyAnimal
    /// - 简化代码逻辑，消除同步问题
    /// </summary>
    public static class PawnStatusKnowledgeGenerator
    {
        // 记录每个Pawn上次更新时间（仅用于控制更新频率）
        private static Dictionary<int, int> lastUpdateTicks = new Dictionary<int, int>();
        private const int UPDATE_INTERVAL_TICKS = 60000; // 24小时 = 60000 ticks
        
        // 描述切换阈值（不再删除记录）
        private const int NEW_COLONIST_THRESHOLD_DAYS = 7;
        
        /// <summary>
        /// 更新所有殖民者的状态常识（每小时检查一次）
        /// 只更新距离上次更新>=24小时的Pawn
        /// ? v3.3.17: 简化逻辑，移除colonistJoinTicks传递
        /// </summary>
        public static void UpdateAllColonistStatus()
        {
            if (!RimTalkMemoryPatchMod.Settings.enablePawnStatusKnowledge)
                return;
            
            var library = MemoryManager.GetCommonKnowledge();
            if (library == null) return;

            int currentTick = Find.TickManager.TicksGame;
            int updatedCount = 0;
            
            // 收集所有殖民者（所有地图 + 商队）
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
                    
                    // 检查是否需要更新（24小时间隔）
                    if (!lastUpdateTicks.TryGetValue(pawnID, out int lastUpdate))
                    {
                        lastUpdate = 0; // 首次更新
                    }
                    
                    int ticksSinceUpdate = currentTick - lastUpdate;
                    
                    if (ticksSinceUpdate >= UPDATE_INTERVAL_TICKS)
                    {
                        UpdatePawnStatusKnowledge(pawn, library, currentTick);
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
        /// ? v3.3.17: 完全依赖RimWorld原生记录，每次实时计算
        /// </summary>
        public static void UpdatePawnStatusKnowledge(Pawn pawn, CommonKnowledgeLibrary library, int currentTick)
        {
            if (pawn == null || library == null) return;

            try
            {
                // 婴儿阶段（<3岁）不生成状态
                if (pawn.RaceProps != null && pawn.RaceProps.Humanlike)
                {
                    float ageYears = pawn.ageTracker.AgeBiologicalYearsFloat;
                    if (ageYears < 3f)
                    {
                        CleanupPawnStatusKnowledge(pawn, library);
                        return;
                    }
                }
                
                // ? v3.3.17: 直接从RimWorld记录计算加入时间（每次实时计算）
                int joinTick = CalculateJoinTick(pawn, currentTick);
                int daysInColony = CalculateDaysInColony(joinTick, currentTick);
                
                // 开发模式日志
                if (Prefs.DevMode && UnityEngine.Random.value < 0.05f)
                {
                    Log.Message($"[PawnStatus] {pawn.LabelShort}: joinTick={joinTick}, currentTick={currentTick}, daysInColony={daysInColony}");
                }

                // 使用唯一标签
                string statusTag = $"殖民者状态,{pawn.LabelShort}";
                
                // ? v3.3.3: 改进查找逻辑，优先使用 targetPawnId 防止改名后重复生成
                var existingEntry = library.Entries.FirstOrDefault(e => 
                    (e.targetPawnId == pawn.thingIDNumber && e.tag.Contains("殖民者状态")) ||
                    (e.tag.Contains(pawn.LabelShort) && e.tag.Contains("殖民者状态"))
                );

                // 生成新内容
                string newContent = GenerateStatusContent(pawn, daysInColony, joinTick);
                float defaultImportance = 0.5f;

                if (existingEntry != null)
                {
                    // 检查是否为用户编辑（绝对不覆盖）
                    if (existingEntry.isUserEdited)
                    {
                        return;
                    }
                    
                    // 再次检查内容特征（双重保险）
                    bool isAutoGenerated = IsAutoGeneratedContent(existingEntry.content);
                    
                    if (isAutoGenerated)
                    {
                        // 只更新自动生成的内容
                        existingEntry.content = newContent;
                        existingEntry.importance = defaultImportance;
                        existingEntry.targetPawnId = pawn.thingIDNumber;
                        // 确保标签也是最新的（如果名字变了）
                        if (!existingEntry.tag.Contains(pawn.LabelShort))
                        {
                             existingEntry.tag = statusTag;
                        }
                        
                        if (Prefs.DevMode && UnityEngine.Random.value < 0.05f)
                        {
                            Log.Message($"[PawnStatus] Updated: {pawn.LabelShort} (days: {daysInColony}) -> {newContent}");
                        }
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
        /// ★ v3.3.18: 计算Pawn的加入时间（直接使用RimWorld原生记录）
        /// 修复：使用强引用 RecordDefOf 替代字符串查找
        /// </summary>
        private static int CalculateJoinTick(Pawn pawn, int currentTick)
        {
            try
            {
                if (pawn.records == null)
                    return currentTick; // 无记录系统，视为刚加入
                
                // ★ v3.3.18: 修复 - 使用强引用替代字符串查找
                // 旧代码（不可靠）：
                // var recordDef = DefDatabase<RecordDef>.GetNamed("TimeAsColonistOrColonyAnimal", false);
                
                // 新代码（强引用）：
                var recordDef = RecordDefOf.TimeAsColonistOrColonyAnimal;
                
                if (recordDef == null)
                {
                    if (Prefs.DevMode)
                        Log.Warning($"[PawnStatus] RecordDef 'TimeAsColonistOrColonyAnimal' not found (this should never happen with strong reference)");
                    return currentTick;
                }
                
                // 获取作为殖民者的时间（单位：ticks）
                float timeAsColonist = pawn.records.GetValue(recordDef);
                
                if (timeAsColonist <= 0)
                {
                    // 刚加入的殖民者，记录为0
                    return currentTick;
                }
                
                // 加入的时间 = 当前时间 - 作为殖民者的时间
                int joinTick = currentTick - (int)timeAsColonist;
                
                // 安全检查：加入时间不能早于游戏开始（初始殖民者）
                if (joinTick < 0)
                {
                    joinTick = 0; // 游戏开始时就存在
                }
                
                return joinTick;
            }
            catch (Exception ex)
            {
                Log.Error($"[PawnStatus] Error calculating join tick for {pawn?.LabelShort}: {ex.Message}");
                return currentTick; // 出错时视为刚加入
            }
        }
        
        /// <summary>
        /// ? v3.3.17: 计算殖民地天数
        /// </summary>
        private static int CalculateDaysInColony(int joinTick, int currentTick)
        {
            int ticksInColony = currentTick - joinTick;
            int daysInColony = ticksInColony / GenDate.TicksPerDay;
            
            // 防止负数
            if (daysInColony < 0)
            {
                Log.Warning($"[PawnStatus] Negative days detected: {daysInColony}, resetting to 0");
                daysInColony = 0;
            }
            
            return daysInColony;
        }

        /// <summary>
        /// 生成状态描述文本（优化为自然人称视角）
        /// ? v3.3.17: 使用实时计算的joinTick
        /// </summary>
        private static string GenerateStatusContent(Pawn pawn, int daysInColony, int joinTick)
        {
            string name = pawn.LabelShort;
            
            // 计算加入日期（游戏内日期）
            int tile = pawn.Map?.Tile ?? (Find.AnyPlayerHomeMap?.Tile ?? 0);
            float longitude = Find.WorldGrid.LongLatOf(tile).x;

            // 使用 DayOfQuadrum (0-14) 并 +1
            int joinDay = GenDate.DayOfQuadrum(joinTick, longitude) + 1;
            Quadrum joinQuadrum = GenDate.Quadrum(joinTick, longitude);
            int joinYear = GenDate.Year(joinTick, longitude);
            
            // 格式化日期（例如：冬季 5日, 5500年）
            string joinDate = $"{joinQuadrum.Label()} {joinDay}日, {joinYear}年";
            
            // 获取完整种族信息（种族+亚种）
            string raceInfo = GetCompleteRaceInfo(pawn);
            
            // 根据天数生成不同描述
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
        public static void CleanupPawnStatusKnowledge(Pawn pawn, CommonKnowledgeLibrary library)
        {
            if (pawn == null || library == null) return;

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
        /// ? v3.3.17: 简化清理逻辑 - 只清理lastUpdateTicks
        /// 不再需要管理colonistJoinTicks
        /// </summary>
        public static void CleanupUpdateRecords()
        {
            // 收集所有存活的殖民者ID
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
            
            // 清理不存在的Pawn的更新记录
            var toRemove = new List<int>();
            
            foreach (var pawnID in lastUpdateTicks.Keys.ToList())
            {
                // 如果在存活列表中，跳过
                if (allColonistIDs.Contains(pawnID))
                    continue;
                
                // 尝试查找这个Pawn
                Pawn pawn = null;
                
                // 检查所有地图中的所有Pawn（包括死亡的）
                foreach (var map in Find.Maps)
                {
                    pawn = map.mapPawns.AllPawns.FirstOrDefault(p => p.thingIDNumber == pawnID);
                    if (pawn != null) break;
                }
                
                // 检查世界Pawns
                if (pawn == null && Find.WorldPawns != null)
                {
                    pawn = Find.WorldPawns.AllPawnsAlive.FirstOrDefault(p => p.thingIDNumber == pawnID);
                }
                
                // 决定是否删除记录
                bool shouldRemove = false;
                
                if (pawn == null)
                {
                    // 找不到Pawn - 可能已经完全消失
                    shouldRemove = true;
                }
                else
                {
                    // 找到Pawn - 检查是否真的应该删除
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
                            Log.Message($"[PawnStatus] Removing non-player pawn: {pawn.LabelShort}");
                    }
                }
                
                if (shouldRemove)
                {
                    toRemove.Add(pawnID);
                }
            }
            
            // 执行删除
            foreach (var id in toRemove)
            {
                lastUpdateTicks.Remove(id);
            }
            
            // 日志输出
            if (toRemove.Count > 0 && Prefs.DevMode)
            {
                Log.Message($"[PawnStatus] Cleaned up {toRemove.Count} update records");
            }
        }
    }
}
