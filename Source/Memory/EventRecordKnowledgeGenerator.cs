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
            { "died", 1.0f }, { "killed", 1.0f }, { "death", 1.0f }, { "dead", 1.0f },
            
            // 战斗相关（重要性0.9）
            { "袭击", 0.9f }, { "进攻", 0.9f }, { "防御", 0.9f }, { "raid", 0.9f }, { "attack", 0.9f },
            { "击退", 0.85f }, { "战胜", 0.85f }, { "defeated", 0.85f },
            
            // ? 新增：葬礼相关（重要性0.9）
            { "葬礼", 0.9f }, { "葬", 0.9f }, { "埋葬", 0.9f }, { "funeral", 0.9f }, { "burial", 0.9f },
            { "举行葬礼", 0.9f }, { "安葬", 0.9f },
            
            // 关系相关（重要性0.85）
            { "结婚", 0.85f }, { "订婚", 0.85f }, { "married", 0.85f }, { "engaged", 0.85f },
            { "婚礼", 0.85f }, { "wedding", 0.85f }, { "举行婚礼", 0.85f },
            { "分手", 0.75f }, { "离婚", 0.75f }, { "breakup", 0.75f },
            
            // ? 新增：生日相关（重要性0.7）
            { "生日", 0.7f }, { "birthday", 0.7f }, { "庆祝", 0.6f }, { "celebration", 0.6f },
            { "过生日", 0.7f }, { "庆祝生日", 0.7f },
            
            // ? 新增：研究突破（重要性0.8）
            { "突破", 0.8f }, { "breakthrough", 0.8f }, { "完成研究", 0.8f }, { "research complete", 0.8f },
            { "研究完成", 0.8f }, { "发明", 0.8f }, { "invention", 0.8f },
            
            // ? 新增：周年纪念（重要性0.7）
            { "周年", 0.7f }, { "anniversary", 0.7f }, { "周年纪念", 0.7f },
            
            // 成员变动（重要性0.8）
            { "加入", 0.8f }, { "逃跑", 0.8f }, { "离开", 0.8f }, { "joined", 0.8f }, { "fled", 0.8f },
            { "招募", 0.75f }, { "recruited", 0.75f }, { "新成员", 0.8f },
            
            // 灾害相关（重要性0.85）
            { "爆炸", 0.85f }, { "烟雾", 0.85f }, { "火灾", 0.85f }, { "explosion", 0.85f }, { "fire", 0.85f },
            { "毒船", 0.85f }, { "龙卷风", 0.85f }, { "tornado", 0.85f },
            { "疾病", 0.85f }, { "饥荒", 0.8f }, { "饿死", 0.8f }, { "starvation", 0.8f },
            
            // ? 新增：其他重要事件
            { "日食", 0.75f }, { "eclipse", 0.75f },
            { "虫族", 0.85f }, { "infestation", 0.85f },
            { "贸易", 0.6f }, { "caravan", 0.6f }, { "visitor", 0.6f },
            { "任务", 0.65f }, { "quest", 0.65f },
        };
        
        /// <summary>
        /// 每小时扫描PlayLog事件
        /// 生成全局公共殖民地历史常识
        /// ⚠️ v3.4.4: 性能优化 - 从尾部倒序扫描，避免全量遍历
        /// </summary>
        public static void ScanRecentPlayLog()
        {
            if (!RimTalkMemoryPatchMod.Settings.enableEventRecordKnowledge)
                return;
            
            try
            {
                var gameHistory = Find.PlayLog;
                if (gameHistory == null || gameHistory.AllEntries == null)
                    return;
                
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return;
                
                // ⭐ v3.3.3: 先更新已有事件常识的时间前缀
                UpdateEventKnowledgeTimePrefix(library);
                
                int processedCount = 0;
                int currentTick = Find.TickManager.TicksGame;
                int hourThreshold = currentTick - GenDate.TicksPerHour;
                
                // ⚠️ v3.4.4: 性能优化关键修复
                // 不再使用 LINQ Where 遍历全部历史，改为从尾部倒序扫描
                // PlayLog.AllEntries 是 List，最新的记录在尾部
                var allEntries = gameHistory.AllEntries;
                int totalCount = allEntries.Count;
                int scannedCount = 0;
                int maxScan = Math.Min(50, totalCount); // 最多扫描50条
                
                // 从尾部（最新）向前遍历
                for (int i = totalCount - 1; i >= 0 && scannedCount < maxScan; i--)
                {
                    try
                    {
                        var logEntry = allEntries[i];
                        if (logEntry == null)
                            continue;
                        
                        scannedCount++;
                        
                        // ⚠️ 关键优化：一旦遇到超过1小时的记录，立即终止
                        if (logEntry.Age < hourThreshold)
                        {
                            // 已经超过时间范围，因为 Age 是递减的，后面的更旧
                            break;
                        }
                        
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
                        
                        // 获取事件信息
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
                                
                                // ⭐ v3.3.3: 提取原始事件文本（移除时间前缀）
                                string originalText = ExtractOriginalEventText(eventText);
                                
                                // ⭐ v3.3.3: 创建事件常识，保存创建时间和原始文本
                                var entry = new CommonKnowledgeEntry("事件,历史", eventText)
                                {
                                    importance = importance,
                                    isEnabled = true,
                                    isUserEdited = false,
                                    creationTick = currentTick,           // ⭐ 设置创建时间戳
                                    originalEventText = originalText      // ⭐ 保存原始文本
                                    // targetPawnId = -1 (默认全局)
                                };
                                
                                library.AddEntry(entry);
                                processedCount++;
                                
                                // ⭐ v3.3.2: 减少日志量 - 仅DevMode且10%概率
                                if (Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                                {
                                    Log.Message($"[EventRecord] Created event knowledge: {eventText.Substring(0, Math.Min(50, eventText.Length))}...");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // ⭐ v3.3.2: 仅在DevMode且随机输出
                        if (Prefs.DevMode && UnityEngine.Random.value < 0.2f)
                        {
                            Log.Warning($"[EventRecord] Error processing log entry: {ex.Message}");
                        }
                    }
                }
                
                // ⭐ v3.3.2: 减少日志量 - 仅DevMode且10%概率
                if (processedCount > 0 && Prefs.DevMode && UnityEngine.Random.value < 0.1f)
                {
                    Log.Message($"[EventRecord] Scanned {scannedCount} entries, processed {processedCount} new events (total: {totalCount})");
                }
            }
            catch (Exception ex)
            {
                // ⭐ v3.3.2: 减少日志量，降低错误频率
                if (Prefs.DevMode && UnityEngine.Random.value < 0.2f)
                {
                    Log.Error($"[EventRecord] Error scanning PlayLog: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// 从LogEntry提取事件信息
        /// ? v3.3.4: 优化为提取关键信息，减少token消耗
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
                
                // ? 修改：允许处理一些重要的非Incident事件，特别是死亡和葬礼
                if (logEntry.GetType().Name == "PlayLogEntry_Incident")
                {
                    // 对于Incident事件，检查是否是需要补充的事件类型
                    string previewText = logEntry.ToGameStringFromPOV(null, false);
                    if (!string.IsNullOrEmpty(previewText))
                    {
                        // 允许处理：死亡、葬礼、结婚等重要事件（作为IncidentPatch的补充）
                        bool isImportantEvent = 
                            previewText.Contains("死亡") || previewText.Contains("died") || previewText.Contains("killed") || 
                            previewText.Contains("dead") || previewText.Contains("death") ||
                            previewText.Contains("葬礼") || previewText.Contains("葬") || previewText.Contains("埋葬") ||
                            previewText.Contains("结婚") || previewText.Contains("婚礼") || previewText.Contains("married") ||
                            previewText.Contains("生日") || previewText.Contains("birthday") ||
                            previewText.Contains("突破") || previewText.Contains("breakthrough");
                        
                        if (!isImportantEvent)
                        {
                            // 其他Incident事件：跳过，避免重复（IncidentPatch已处理）
                            return null;
                        }
                        // 重要事件：继续处理
                    }
                    else
                    {
                        return null;
                    }
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
                
                // ? 增强关键词检测：检查是否包含重要关键词
                bool hasImportantKeyword = ImportantKeywords.Keys.Any(k => text.Contains(k));
                
                if (!hasImportantKeyword)
                {
                    // ? 新增：如果没有匹配重要关键词，但这是Incident事件，也记录（宽松模式）
                    if (logEntry.GetType().Name == "PlayLogEntry_Incident")
                    {
                        // 移除调试日志
                        // 对于Incident事件，即使关键词不匹配也记录（但降低重要性）
                    }
                    else
                    {
                        return null; // 非Incident事件必须有关键词匹配
                    }
                }
                
                // ? 过滤对话内容：如果包含对话标记，跳过
                if (IsConversationContent(text))
                    return null;
                
                // ? v3.3.4: 提取关键信息（压缩原文）
                string compressedText = ExtractKeyInformation(text);
                
                if (string.IsNullOrEmpty(compressedText))
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
                
                return $"{timePrefix}{compressedText}";
            }
            catch (Exception ex)
            {
                // ? v3.3.2: 移除调试日志
                // if (Prefs.DevMode) { ... }
                return null;
            }
        }
        
        /// <summary>
        /// ? v3.3.10: 提取关键信息，压缩事件文本（修复制造清单问题）
        /// 目标：将详细事件描述压缩为最重要的部分，减少token浪费
        /// 示例：
        /// - "小明在机械加工台上种植了12株玉米" → "小明种植玉米x12"
        /// - "张三击杀了袭击者（机械虫）" → "张三击杀机械虫"
        /// - "李四运输物资送到仓库" → "李四运输物资"
        /// - "王五完成target.A清单-手工制作台" → "王五在手工制作台制造物品"
        /// </summary>
        private static string ExtractKeyInformation(string fullText)
        {
            if (string.IsNullOrEmpty(fullText))
                return null;
            
            try
            {
                // ? v3.3.10: 修复1 - 先检测并清理 "完成...清单" 模式
                fullText = CleanBillText(fullText);
                
                // 1. 提取主要人物（第一个出现的人名，通常是主语）
                string mainPerson = ExtractMainPerson(fullText);
                
                // 2. 提取核心动作（最重要的关键词）
                string action = ExtractMainAction(fullText);
                
                // 3. 提取目标/对象（第二个人名或者重要名词）
                string target = ExtractTarget(fullText, mainPerson);
                
                // 4. 提取数量信息（如 "x12"）
                string quantity = ExtractQuantity(fullText);
                
                // 5. 组合压缩文本
                var parts = new List<string>();
                
                if (!string.IsNullOrEmpty(mainPerson))
                    parts.Add(mainPerson);
                
                if (!string.IsNullOrEmpty(action))
                    parts.Add(action);
                
                if (!string.IsNullOrEmpty(target))
                    parts.Add(target);
                
                if (!string.IsNullOrEmpty(quantity))
                    parts.Add(quantity);
                
                // 如果提取失败，返回原文的截断
                if (parts.Count < 2)
                {
                    // 至少保证主语和动作，否则使用原文（截断）
                    return fullText.Length > 40 ? fullText.Substring(0, 40) : fullText;
                }
                
                return string.Join("", parts);
            }
            catch (Exception ex)
            {
                // 提取失败，返回原文截断
                if (Prefs.DevMode)
                    Log.Warning($"[EventRecord] Key info extraction failed: {ex.Message}");
                
                return fullText.Length > 40 ? fullText.Substring(0, 40) : fullText;
            }
        }
        
        /// <summary>
        /// ? v3.3.10: 清理制造清单相关的无用文本
        /// "完成target.A清单-手工制作台" → "在手工制作台制造物品"
        /// </summary>
        private static string CleanBillText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            
            try
            {
                // 匹配模式："完成...清单-工作台名称"
                var billPattern = System.Text.RegularExpressions.Regex.Match(
                    text, 
                    @"完成(target\.[A-Za-z0-9]+|.+?)清单[－\-](.+?)(?:[，。、\s]|$)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                
                if (billPattern.Success)
                {
                    string workbenchName = billPattern.Groups[2].Value.Trim();
                    
                    // 清理工作台名称中的无用字符
                    workbenchName = System.Text.RegularExpressions.Regex.Replace(
                        workbenchName, 
                        @"[（\(].*?[）\)]", 
                        ""
                    ).Trim();
                    
                    // 提取主语（人名）
                    string[] words = text.Split(new[] { '完', '成', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    string person = words.Length > 0 ? words[0].Trim() : "";
                    
                    // 重构为："人名在工作台制造物品"
                    if (!string.IsNullOrEmpty(person) && !string.IsNullOrEmpty(workbenchName))
                    {
                        return $"{person}在{workbenchName}制造物品";
                    }
                    else if (!string.IsNullOrEmpty(workbenchName))
                    {
                        return $"在{workbenchName}制造物品";
                    }
                }
                
                // 如果没有匹配，检查是否包含 target.* 模式，直接删除
                text = System.Text.RegularExpressions.Regex.Replace(
                    text,
                    @"target\.[A-Za-z0-9]+",
                    "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                
                return text.Trim();
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[EventRecord] Bill text cleaning failed: {ex.Message}");
                
                return text;
            }
        }
        
        /// <summary>
        /// 提取主要人物（通常是事件的主体）
        /// </summary>
        private static string ExtractMainPerson(string text)
        {
            // 方法1：查找常见名字分隔符前的文本
            var separators = new[] { "在", "的", "将", "被", "向", "与", "和", "对", " " };
            
            foreach (var sep in separators)
            {
                int index = text.IndexOf(sep);
                if (index > 0 && index < 15) // 名字通常在前15字符内
                {
                    string candidate = text.Substring(0, index).Trim();
                    
                    // 验证是否像名字（2-8字符，无标点）
                    if (candidate.Length >= 2 && candidate.Length <= 8 && 
                        !candidate.Any(c => char.IsPunctuation(c) || char.IsDigit(c)))
                    {
                        return candidate;
                    }
                }
            }
            
            // 方法2：如果找不到，取前5个字符（可能是名字）
            if (text.Length >= 2)
            {
                string candidate = text.Substring(0, Math.Min(5, text.Length)).Trim();
                if (!candidate.Any(c => char.IsPunctuation(c)))
                    return candidate;
            }
            
            return null;
        }
        
        /// <summary>
        /// 提取核心动作（基于重要关键词）
        /// </summary>
        private static string ExtractMainAction(string text)
        {
            // 查找文本中的重要关键词
            var matchedKeywords = ImportantKeywords.Keys
                .Where(k => text.Contains(k))
                .OrderByDescending(k => ImportantKeywords[k]) // 按重要性排序
                .ThenByDescending(k => k.Length) // 优先长关键词
                .ToList();
            
            if (matchedKeywords.Any())
            {
                // 返回最重要的关键词作为核心动作
                return matchedKeywords.First();
            }
            
            // 如果没有匹配关键词，尝试提取动词（简单启发式）
            var commonActions = new[] { "种植", "建造", "完成", "击杀", "攻击", "防御", "制作", "烹饪", 
                                         "研究", "治疗", "加入", "离开", "死亡", "受伤" };
            
            foreach (var action in commonActions)
            {
                if (text.Contains(action))
                    return action;
            }
            
            return null;
        }
        
        /// <summary>
        /// 提取目标/对象（通常是动作的接受者）
        /// </summary>
        private static string ExtractTarget(string text, string mainPerson)
        {
            // 移除主要人物后，查找第二个名字或重要名词
            string remaining = text;
            if (!string.IsNullOrEmpty(mainPerson))
            {
                int index = text.IndexOf(mainPerson);
                if (index >= 0)
                {
                    remaining = text.Substring(index + mainPerson.Length);
                }
            }
            
            // 查找常见目标标记词后的内容
            var targetMarkers = new[] { "击杀", "攻击", "种植", "建造", "制作", "完成", "治疗", "了" };
            
            foreach (var marker in targetMarkers)
            {
                int markerIndex = remaining.IndexOf(marker);
                if (markerIndex >= 0)
                {
                    // 提取标记词后的10字符
                    int start = markerIndex + marker.Length;
                    if (start < remaining.Length)
                    {
                        string targetText = remaining.Substring(start, Math.Min(15, remaining.Length - start));
                        
                        // 清理标点和多余文字
                        targetText = targetText.Split(new[] { '，', '。', '、', '；', '的', '在', '和', ' ' })[0].Trim();
                        
                        if (targetText.Length >= 2 && targetText.Length <= 10)
                        {
                            return targetText;
                        }
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 提取数量信息（如 "×12"）
        /// </summary>
        private static string ExtractQuantity(string text)
        {
            // 查找数字
            var match = System.Text.RegularExpressions.Regex.Match(text, @"\d+");
            
            if (match.Success)
            {
                int number = int.Parse(match.Value);
                
                // 只保留有意义的数量（>1)
                if (number > 1 && number < 10000)
                {
                    return $"×{number}";
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// 用于清理记录和维护性能
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
        
        /// <summary>
        /// ? v3.3.3: 更新事件常识的时间前缀（动态更新"今天" → "3天前"）
        /// </summary>
        private static void UpdateEventKnowledgeTimePrefix(CommonKnowledgeLibrary library)
        {
            if (library == null)
                return;
            
            try
            {
                int currentTick = Find.TickManager.TicksGame;
                int updatedCount = 0;
                
                // 查找所有事件常识
                var eventEntries = library.Entries
                    .Where(e => e.tag != null && (e.tag.Contains("事件") || e.tag.Contains("历史")))
                    .Where(e => e.creationTick >= 0) // 只更新有时间戳的
                    .ToList();
                
                foreach (var entry in eventEntries)
                {
                    // 更新时间前缀
                    entry.UpdateEventTimePrefix(currentTick);
                    updatedCount++;
                }
                
                // ? 日志：记录更新操作（仅DevMode且低频率）
                if (updatedCount > 0 && Prefs.DevMode && UnityEngine.Random.value < 0.05f)
                {
                    Log.Message($"[EventRecord] Updated time prefix for {updatedCount} event knowledge entries");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EventRecord] Error updating event time prefix: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ? v3.3.3: 从带时间前缀的事件文本中提取原始文本
        /// </summary>
        private static string ExtractOriginalEventText(string eventText)
        {
            if (string.IsNullOrEmpty(eventText))
                return eventText;
            
            // 移除常见的时间前缀
            string[] timePrefixes = { "今天", "1天前", "2天前", "3天前", "4天前", "5天前", "6天前",
                                     "约3天前", "约4天前", "约5天前", "约6天前", "约7天前" };
            
            foreach (var prefix in timePrefixes)
            {
                if (eventText.StartsWith(prefix))
                {
                    return eventText.Substring(prefix.Length);
                }
            }
            
            return eventText;
        }
        
        /// <summary>
        /// ? 新增：检测是否是对话内容（避免记录对话）
        /// </summary>
        private static bool IsConversationContent(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
            
            // 检测对话标记
            string[] conversationMarkers = { 
                "说:", "said:", "说：", "说道:", "说道：",
                "问:", "asked:", "问：", "问道:", "问道：",
                "回答:", "replied:", "回答：", "答道:", "答道：",
                "叫道:", "shouted:", "叫道：", "喊道:", "喊道："
            };
            
            return conversationMarkers.Any(marker => text.Contains(marker));
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
            
            // ? 新增：没有关键词匹配但可能是Incident事件，给较低默认重要性
            return 0.4f; // 比普通事件低，但仍会被记录
        }
    }
}
