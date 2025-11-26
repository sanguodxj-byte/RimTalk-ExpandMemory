using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// 事件记录常识生成器 (PlayLog扫描 - 补充监听)
    /// ? 职责：捕获IncidentPatch无法监听的事件
    /// - 死亡通知（非Incident触发的死亡）
    /// - 关系变化细节（包含参与者名字）
    /// - 其他重要日志（兜底机制）
    /// </summary>
    public static class EventRecordKnowledgeGenerator
    {
        // 已处理的记录ID（防止重复）
        private static HashSet<int> processedLogIDs = new HashSet<int>();
        
        // 重要事件关键词（优先级）
        private static readonly Dictionary<string, float> ImportantKeywords = new Dictionary<string, float>
        {
            // 死亡相关（最重要1.0）
            { "死亡", 1.0f }, { "倒下", 1.0f }, { "被杀", 1.0f }, { "击杀", 1.0f }, { "牺牲", 1.0f },
            { "died", 1.0f }, { "killed", 1.0f }, { "death", 1.0f },
            
            // 战斗相关（重要性0.9）
            { "袭击", 0.9f }, { "进攻", 0.9f }, { "防御", 0.9f }, { "raid", 0.9f }, { "attack", 0.9f },
            { "击退", 0.85f }, { "战胜", 0.85f }, { "defeated", 0.85f },
            
            // 关系相关（重要性0.85）
            { "结婚", 0.85f }, { "订婚", 0.85f }, { "married", 0.85f }, { "engaged", 0.85f },
            { "分手", 0.75f }, { "离婚", 0.75f }, { "breakup", 0.75f },
            
            // 成员变动（重要性0.8）
            { "加入", 0.8f }, { "逃跑", 0.8f }, { "离开", 0.8f }, { "joined", 0.8f }, { "fled", 0.8f },
            { "招募", 0.75f }, { "recruited", 0.75f },
            
            // 灾难相关（重要性0.85）
            { "爆炸", 0.85f }, { "起火", 0.85f }, { "崩溃", 0.85f }, { "explosion", 0.85f }, { "fire", 0.85f },
            { "饥荒", 0.85f }, { "饿死", 0.8f }, { "冻死", 0.8f },
        };
        
        /// <summary>
        /// 每小时扫描PlayLog事件
        /// 生成全局共享的殖民地历史常识
        /// </summary>
        public static void ScanRecentPlayLog()
        {
            if (!RimTalkMemoryPatchMod.Settings.enableEventRecordKnowledge)
                return;
            
            try
            {
                var gameHistory = Find.PlayLog;
                if (gameHistory == null)
                    return;
                
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return;
                
                int processedCount = 0;
                int currentTick = Find.TickManager.TicksGame;
                
                // 只处理最近1小时的事件
                int oneHourAgo = currentTick - 2500;
                
                var recentEntries = gameHistory.AllEntries
                    .Where(e => e != null && e.Age > oneHourAgo)
                    .OrderByDescending(e => e.Age)
                    .Take(50);
                
                foreach (var logEntry in recentEntries)
                {
                    try
                    {
                        // 使用LogEntry的ID去重
                        int logID = logEntry.GetHashCode();
                        
                        if (processedLogIDs.Contains(logID))
                            continue;
                        
                        processedLogIDs.Add(logID);
                        
                        // 控制集合大小
                        if (processedLogIDs.Count > 2000)
                        {
                            var toRemove = processedLogIDs.Take(1000).ToList();
                            foreach (var id in toRemove)
                            {
                                processedLogIDs.Remove(id);
                            }
                        }
                        
                        // 提取事件信息
                        string eventText = ExtractEventInfo(logEntry);
                        
                        if (!string.IsNullOrEmpty(eventText))
                        {
                            // 检查是否已存在
                            bool exists = library.Entries.Any(e => 
                                e.content.Contains(eventText.Substring(0, Math.Min(15, eventText.Length)))
                            );
                            
                            if (!exists)
                            {
                                // 计算重要性
                                float importance = CalculateImportance(eventText);
                                
                                // ? 创建全局常识（targetPawnId保持默认-1）
                                var entry = new CommonKnowledgeEntry("事件,历史", eventText)
                                {
                                    importance = importance,
                                    isEnabled = true,
                                    isUserEdited = false
                                    // targetPawnId = -1 (默认全局)
                                };
                                
                                library.AddEntry(entry);
                                processedCount++;
                                
                                if (Prefs.DevMode)
                                {
                                    Log.Message($"[EventRecord] Created global event knowledge: {eventText.Substring(0, Math.Min(50, eventText.Length))}...");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warning($"[EventRecord] Error processing log entry: {ex.Message}");
                    }
                }
                
                if (processedCount > 0 && Prefs.DevMode)
                {
                    Log.Message($"[EventRecord] Processed {processedCount} new PlayLog events");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EventRecord] Error scanning PlayLog: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 从LogEntry提取事件信息
        /// </summary>
        private static string ExtractEventInfo(LogEntry logEntry)
        {
            if (logEntry == null)
                return null;
            
            try
            {
                // 跳过对话类型的日志（已由RimTalk对话记忆处理）
                if (logEntry.GetType().Name == "PlayLogEntry_Interaction")
                {
                    return null;
                }
                
                // ? 跳过IncidentPatch已处理的事件类型
                if (logEntry.GetType().Name == "PlayLogEntry_Incident")
                {
                    // IncidentPatch会实时处理，无需重复
                    return null;
                }
                
                string text = logEntry.ToGameStringFromPOV(null, false);
                
                if (string.IsNullOrEmpty(text))
                    return null;
                
                // 过滤长度
                if (text.Length < 10 || text.Length > 200)
                    return null;
                
                // 过滤无聊事件
                if (IsBoringMessage(text))
                    return null;
                
                // 检查是否包含重要关键词
                bool hasImportantKeyword = ImportantKeywords.Keys.Any(k => text.Contains(k));
                
                if (!hasImportantKeyword)
                    return null;
                
                // 添加时间前缀
                int ticksAgo = Find.TickManager.TicksGame - logEntry.Age;
                int daysAgo = ticksAgo / GenDate.TicksPerDay;
                
                string timePrefix = "";
                if (daysAgo < 1)
                {
                    timePrefix = "今天";
                }
                else if (daysAgo < 3)
                {
                    timePrefix = $"{daysAgo}天前";
                }
                else if (daysAgo < 7)
                {
                    timePrefix = $"约{daysAgo}天前";
                }
                else
                {
                    return null; // 超过7天的事件不记录
                }
                
                return $"{timePrefix}{text}";
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[EventRecord] Error in ExtractEventInfo: {ex.Message}");
                }
                return null;
            }
        }
        
        /// <summary>
        /// 过滤无聊事件
        /// </summary>
        private static bool IsBoringMessage(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;
            
            var boringKeywords = new[] 
            { 
                "走路", "吃饭", "睡觉", "娱乐", "闲逛", "休息",
                "walking", "eating", "sleeping", "recreation", "wandering"
            };
            
            return boringKeywords.Any(k => text.Contains(k));
        }
        
        /// <summary>
        /// 计算事件重要性
        /// </summary>
        private static float CalculateImportance(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0.5f;
            
            // 找到匹配的关键词
            var matched = ImportantKeywords
                .Where(kv => text.Contains(kv.Key))
                .OrderByDescending(kv => kv.Value)
                .FirstOrDefault();
            
            if (matched.Key != null)
            {
                return matched.Value;
            }
            
            return 0.6f; // 默认重要性
        }
        
        /// <summary>
        /// 清理过期记录（维护性能）
        /// </summary>
        public static void CleanupProcessedRecords()
        {
            // 控制集合大小
            if (processedLogIDs.Count > 2000)
            {
                var toRemove = processedLogIDs.Take(1000).ToList();
                foreach (var id in toRemove)
                {
                    processedLogIDs.Remove(id);
                }
            }
        }
    }
}
