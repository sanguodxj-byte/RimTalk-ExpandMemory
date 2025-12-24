using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;
using RimWorld;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// 监听游戏事件（Incident）系统，实时捕获重要事件
    /// ? 支持两阶段事件记录：袭击到来 → 击退更新
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class IncidentPatch
    {
        // ? 追踪活跃的袭击事件（用于后续更新）
        private static Dictionary<int, RaidEventInfo> activeRaids = new Dictionary<int, RaidEventInfo>();
        
        [HarmonyPostfix]
        public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            // 只处理成功执行的事件
            if (!__result)
                return;
            
            // 检查设置是否启用
            if (!RimTalk.MemoryPatch.RimTalkMemoryPatchMod.Settings.enableEventRecordKnowledge)
                return;
            
            try
            {
                var incidentDef = __instance.def;
                if (incidentDef == null)
                    return;
                
                // ? 特殊处理袭击事件
                if (IsRaidIncident(incidentDef))
                {
                    HandleRaidStart(incidentDef, parms);
                    return;
                }
                
                // 分析事件类型和重要性
                float importance = CalculateIncidentImportance(incidentDef);
                
                // 只记录重要事件
                if (importance < 0.5f)
                    return;
                
                // 生成事件描述
                string eventText = GenerateEventDescription(incidentDef, parms);
                
                if (string.IsNullOrEmpty(eventText))
                    return;
                
                // 添加到常识库
                AddOrUpdateKnowledge(null, eventText, importance);
            }
            catch (Exception ex)
            {
                Log.Error($"[EventRecord] Error in IncidentPatch: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ? 判断是否是袭击事件
        /// </summary>
        private static bool IsRaidIncident(IncidentDef incidentDef)
        {
            string defName = incidentDef.defName;
            return defName.Contains("Raid") || 
                   defName.Contains("Siege") || 
                   defName.Contains("Mech") && defName.Contains("Cluster") ||
                   incidentDef.category == IncidentCategoryDefOf.ThreatBig;
        }
        
        /// <summary>
        /// ? 处理袭击开始
        /// </summary>
        private static void HandleRaidStart(IncidentDef incidentDef, IncidentParms parms)
        {
            // 生成袭击ID（用于后续更新）
            int raidId = GenTicks.TicksGame;
            
            // 获取派系信息
            string factionName = "未知敌人";
            if (parms.faction != null && !string.IsNullOrEmpty(parms.faction.Name))
            {
                factionName = parms.faction.Name;
            }
            
            // 获取袭击类型
            string raidType = GetRaidType(incidentDef);
            
            // 生成初始描述
            string eventText = $"今天{factionName}发动了{raidType}";
            
            // 添加到常识库
            var entry = AddOrUpdateKnowledge(null, eventText, 0.9f);
            
            // 记录活跃袭击信息
            if (entry != null)
            {
                activeRaids[raidId] = new RaidEventInfo
                {
                    entryId = entry.id,
                    factionName = factionName,
                    raidType = raidType,
                    startTick = GenTicks.TicksGame,
                    initialText = eventText
                };
                
                // 启动监听（检查袭击结束）
                if (!raidCheckActive)
                {
                    raidCheckActive = true;
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[EventRecord] ?? Raid started: {eventText} (ID: {raidId})");
                }
            }
        }
        
        /// <summary>
        /// ? 获取袭击类型
        /// </summary>
        private static string GetRaidType(IncidentDef incidentDef)
        {
            string defName = incidentDef.defName;
            
            if (defName.Contains("Siege"))
                return "围城";
            else if (defName.Contains("Mech"))
                return "机械族攻击";
            else if (defName.Contains("Sapper"))
                return "工兵袭击";
            else if (defName.Contains("Breacher"))
                return "破坏者袭击";
            else
                return "袭击";
        }
        
        /// <summary>
        /// ? 检查袭击状态（每小时调用一次）
        /// </summary>
        private static bool raidCheckActive = false;
        
        public static void CheckRaidStatus()
        {
            if (!raidCheckActive || activeRaids.Count == 0)
                return;
            
            try
            {
                var library = RimTalk.Memory.MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return;
                
                int currentTick = GenTicks.TicksGame;
                var completedRaids = new List<int>();
                
                foreach (var kvp in activeRaids)
                {
                    int raidId = kvp.Key;
                    var raidInfo = kvp.Value;
                    
                    // 检查是否超时（超过4小时视为结束）
                    int elapsedTicks = currentTick - raidInfo.startTick;
                    if (elapsedTicks > 10000) // 4小时 = 2500 * 4
                    {
                        // 超时，判定为击退
                        UpdateRaidOutcome(library, raidInfo, true);
                        completedRaids.Add(raidId);
                    }
                    else
                    {
                        // 检查地图上是否还有敌人
                        bool hasEnemies = CheckForEnemies();
                        
                        if (!hasEnemies && elapsedTicks > 1000) // 至少持续一段时间才算击退
                        {
                            // 击退成功
                            UpdateRaidOutcome(library, raidInfo, true);
                            completedRaids.Add(raidId);
                        }
                    }
                }
                
                // 清理已完成的袭击
                foreach (var raidId in completedRaids)
                {
                    activeRaids.Remove(raidId);
                }
                
                // 如果没有活跃袭击，停止检查
                if (activeRaids.Count == 0)
                {
                    raidCheckActive = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EventRecord] Error checking raid status: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ? 检查地图上是否还有敌对生物
        /// </summary>
        private static bool CheckForEnemies()
        {
            if (Find.CurrentMap == null)
                return false;
            
            foreach (var pawn in Find.CurrentMap.mapPawns.AllPawnsSpawned)
            {
                if (pawn.HostileTo(Faction.OfPlayer) && !pawn.Dead && !pawn.Downed)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// ? 更新袭击结果
        /// </summary>
        private static void UpdateRaidOutcome(CommonKnowledgeLibrary library, RaidEventInfo raidInfo, bool defeated)
        {
            // 查找原始条目
            var entry = library.Entries.FirstOrDefault(e => e.id == raidInfo.entryId);
            
            if (entry == null)
            {
                // 条目被删除了，直接返回
                return;
            }
            
            // 更新内容
            if (defeated)
            {
                entry.content = $"{raidInfo.initialText}，殖民地成功击退了进攻";
                entry.importance = 0.95f; // 提高重要性
            }
            else
            {
                entry.content = $"{raidInfo.initialText}，造成了严重损失";
                entry.importance = 1.0f; // 最高重要性
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[EventRecord] ? Updated raid outcome: {entry.content}");
            }
        }
        
        /// <summary>
        /// 添加或更新常识
        /// </summary>
        private static CommonKnowledgeEntry AddOrUpdateKnowledge(string existingId, string eventText, float importance)
        {
            var library = RimTalk.Memory.MemoryManager.GetCommonKnowledge();
            if (library == null)
                return null;
            
            CommonKnowledgeEntry entry = null;
            
            // 如果提供了ID，尝试更新现有条目
            if (!string.IsNullOrEmpty(existingId))
            {
                entry = library.Entries.FirstOrDefault(e => e.id == existingId);
                if (entry != null)
                {
                    entry.content = eventText;
                    entry.importance = importance;
                    return entry;
                }
            }
            
            // 检查是否已存在相似内容
            bool exists = library.Entries.Any(e => 
                e.content.Contains(eventText.Substring(0, Math.Min(15, eventText.Length)))
            );
            
            if (!exists)
            {
                entry = new CommonKnowledgeEntry("事件,历史", eventText)
                {
                    importance = importance,
                    isEnabled = true,
                    isUserEdited = false
                };
                
                library.AddEntry(entry);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[EventRecord] ? Created knowledge: {eventText} (importance: {importance:F2})");
                }
            }
            
            return entry;
        }
        
        /// <summary>
        /// 计算事件重要性
        /// </summary>
        private static float CalculateIncidentImportance(IncidentDef incidentDef)
        {
            string defName = incidentDef.defName;
            string label = incidentDef.label;
            
            // 袭击相关在HandleRaidStart中处理，这里不再判断
            
            // 死亡相关（最重要1.0）
            if (defName.Contains("Death") || defName.Contains("Dead") || 
                label.Contains("死") || label.Contains("death"))
                return 1.0f;
            
            // 关系变化（重要性0.85）
            if (defName.Contains("Marriage") || defName.Contains("Wedding") || 
                label.Contains("结婚") || label.Contains("婚"))
                return 0.85f;
            
            // ? 新增：葬礼相关（重要性0.9）
            if (defName.Contains("Funeral") || defName.Contains("Burial") || 
                label.Contains("葬礼") || label.Contains("葬") || label.Contains("埋葬"))
                return 0.9f;
            
            // ? 新增：生日相关（重要性0.7）
            if (defName.Contains("Birthday") || label.Contains("生日"))
                return 0.7f;
            
            // ? 新增：研究突破（重要性0.8）
            if (defName.Contains("Breakthrough") || defName.Contains("Research") && defName.Contains("Complete") ||
                label.Contains("突破") || label.Contains("完成研究"))
                return 0.8f;
            
            // ? 新增：周年纪念（重要性0.7）
            if (defName.Contains("Anniversary") || label.Contains("周年"))
                return 0.7f;
            
            // 成员变动（重要性0.8）
            if (defName.Contains("Join") || defName.Contains("Refugee") || 
                defName.Contains("WandererJoin") || 
                label.Contains("加入") || label.Contains("难民"))
                return 0.8f;
            
            // 虫族
            if (defName.Contains("Infestation") || label.Contains("虫"))
                return 0.85f;
            
            // 灾难（重要性0.85）
            if (defName.Contains("Fire") || defName.Contains("Explosion") || 
                defName.Contains("Tornado") || defName.Contains("Eclipse") ||
                label.Contains("火") || label.Contains("爆炸") || label.Contains("龙卷风"))
                return 0.85f;
            
            // 贸易/访客（重要性0.6）
            if (defName.Contains("Caravan") || defName.Contains("Visitor") || 
                defName.Contains("Trade") ||
                label.Contains("贸易") || label.Contains("访客"))
                return 0.6f;
            
            // 疾病（重要性0.7）
            if (defName.Contains("Disease") || label.Contains("疾病") || label.Contains("瘟疫"))
                return 0.75f;
            
            // 任务完成（重要性0.65）
            if (defName.Contains("Quest") || label.Contains("任务"))
                return 0.65f;
            
            // 其他低优先级事件
            return 0.3f;
        }
        
        /// <summary>
        /// 生成事件描述（非袭击事件）
        /// </summary>
        private static string GenerateEventDescription(IncidentDef incidentDef, IncidentParms parms)
        {
            string label = incidentDef.label;
            string defName = incidentDef.defName;
            
            // 添加时间前缀
            string timePrefix = "今天";
            
            // 处理特殊事件类型
            if (defName.Contains("Marriage") || defName.Contains("Wedding"))
            {
                return $"{timePrefix}举行了婚礼";
            }
            else if (defName.Contains("Funeral") || defName.Contains("Burial"))
            {
                return $"{timePrefix}举行了葬礼";
            }
            else if (defName.Contains("Birthday"))
            {
                return $"{timePrefix}庆祝了生日";
            }
            else if (defName.Contains("Breakthrough") || defName.Contains("Research") && defName.Contains("Complete"))
            {
                return $"{timePrefix}取得了研究突破";
            }
            else if (defName.Contains("Anniversary"))
            {
                return $"{timePrefix}庆祝了周年纪念";
            }
            else if (defName.Contains("WandererJoin") || defName.Contains("RefugeeJoin"))
            {
                return $"{timePrefix}有新成员加入殖民地";
            }
            else if (defName.Contains("Infestation"))
            {
                return $"{timePrefix}发生了虫族入侵";
            }
            else if (defName.Contains("Fire"))
            {
                return $"{timePrefix}发生了火灾";
            }
            else if (defName.Contains("Explosion"))
            {
                return $"{timePrefix}发生了爆炸";
            }
            else if (defName.Contains("Tornado"))
            {
                return $"{timePrefix}遭遇了龙卷风";
            }
            else if (defName.Contains("Eclipse"))
            {
                return $"{timePrefix}发生了日食";
            }
            else if (defName.Contains("TraderCaravan") || defName.Contains("VisitorGroup"))
            {
                // 贸易/访客通常不记录
                return null;
            }
            
            // 通用描述：使用游戏本地化的label
            if (!string.IsNullOrEmpty(label))
            {
                return $"{timePrefix}{label}";
            }
            
            return null;
        }
        
        /// <summary>
        /// ? 袭击事件信息
        /// </summary>
        private class RaidEventInfo
        {
            public string entryId;          // 常识条目ID
            public string factionName;      // 派系名称
            public string raidType;         // 袭击类型
            public int startTick;           // 开始时间
            public string initialText;      // 初始描述
        }
    }
}
