using System;
using System.Text;
using System.Text.RegularExpressions;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 上下文清理器
    /// 用于在向量匹配前提取核心语义内容，去除RimTalk格式噪音
    /// </summary>
    public static class ContextCleaner
    {
        /// <summary>
        /// 为向量匹配清理上下文
        /// </summary>
        public static string CleanForVectorMatching(string context)
        {
            if (string.IsNullOrEmpty(context))
                return "";

            // 1. 处理玩家直接对话
            // 格式: 秩序超凡智能() said to 'Renata: 你知道黄金色的巨树叫什么吗'.Generate...
            if (context.Contains(" said to '"))
            {
                // 尝试匹配带说话人前缀的格式 (例如 'Renata: 内容')
                // 使用 [^']+ 匹配除单引号外的所有字符，确保匹配到正确的引号结束位置
                var match = Regex.Match(context, @"said to '[^']*?: ([^']+)'");
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
                
                // 如果没有冒号，尝试直接提取引号内的内容
                match = Regex.Match(context, @"said to '([^']+)'");
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            StringBuilder sb = new StringBuilder();

            // 2. 处理事件列表 [Ongoing events] ... [Event list end]
            // 这种情况下我们需要保留事件内容
            int eventStart = context.IndexOf("[Ongoing events]");
            int eventEnd = context.IndexOf("[Event list end]");
            
            if (eventStart >= 0 && eventEnd > eventStart)
            {
                // 提取事件部分
                string events = context.Substring(eventStart, eventEnd - eventStart + "[Event list end]".Length);
                sb.AppendLine(events);
            }

            // 3. 提取关键信息行
            // 遍历每一行，保留特定类型的行，过滤掉环境噪音
            string[] lines = context.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            
            bool insideConversationBlock = false;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                
                // 跳过空行
                if (string.IsNullOrEmpty(trimmed)) continue;
                
                // 跳过已经在上面处理过的事件块标记
                if (trimmed.Contains("[Ongoing events]") || trimmed.Contains("[Event list end]")) continue;
                
                // 如果在事件块内部，上面已经提取了，这里跳过
                if (eventStart >= 0 && context.IndexOf(line) > eventStart && context.IndexOf(line) < eventEnd)
                    continue;
                
                // 检测对话/独白块的开始
                // 格式: "PawnName starts conversation..." 或 "PawnName short monologue"
                if (trimmed.Contains("starts conversation") || trimmed.Contains("short monologue"))
                {
                    insideConversationBlock = true;
                    continue; // 跳过起始行本身
                }
                
                // 检测对话/独白块的结束（遇到 Pawn 状态行）
                // 格式: "PawnName(Age:..."
                if (insideConversationBlock && IsPawnStatusLine(trimmed))
                {
                    insideConversationBlock = false;
                    continue; // 跳过状态行
                }
                
                // 如果在对话块内，保留所有非噪音行（这就是用户想要的“中间不固定的各类随机信息”）
                if (insideConversationBlock)
                {
                    if (!IsNoiseLine(trimmed))
                    {
                        sb.AppendLine(trimmed);
                    }
                    continue;
                }
                
                // --- 以下是常规处理（不在特定块内）---

                // 保留 "new good feeling:" (兼容旧逻辑，双重保险)
                if (trimmed.StartsWith("new good feeling:", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine(trimmed);
                    continue;
                }
                
                // 过滤掉环境噪音
                if (IsNoiseLine(trimmed))
                    continue;
                
                // 如果不是噪音，也不是特殊标记，可能是对话内容或独白
                // 但要注意不要把 Pawn 状态信息加进去
                if (!IsPawnStatusLine(trimmed))
                {
                    // 只有当它看起来像是有意义的文本时才添加
                    // 避免添加指令性文本
                    if (!trimmed.Contains("Generate dialogue starting after") &&
                        !trimmed.Contains("Do not generate any further lines"))
                    {
                        sb.AppendLine(trimmed);
                    }
                }
            }
            
            string result = sb.ToString().Trim();
            
            // 如果提取结果为空（可能全是噪音），为了防止向量匹配完全失效，
            // 我们可以返回原始文本（或者至少返回非噪音部分）。
            // 但根据用户需求，他希望“只有我的话去匹配向量”，所以如果提取为空，可能意味着没有有效信息。
            // 这里我们返回提取结果，如果为空，调用者可以决定是否回退。
            
            return result;
        }

        private static bool IsNoiseLine(string line)
        {
            // 时间、天气、位置等
            if (line.StartsWith("Time:")) return true;
            if (line.StartsWith("Today:")) return true;
            if (line.StartsWith("Season:")) return true;
            if (line.StartsWith("Weather:")) return true;
            if (line.StartsWith("Location:")) return true;
            if (line.StartsWith("Terrain:")) return true;
            if (line.StartsWith("Wealth:")) return true;
            if (line.StartsWith("Nearby:")) return true;
            if (line.StartsWith("Nearby people:")) return true;
            if (line.StartsWith("in ChineseSimplified")) return true;
            
            return false;
        }

        private static bool IsPawnStatusLine(string line)
        {
            // 匹配 Pawn 状态行，如: Pratt(Age:34;女性;ID:Colonist;人类) 闲逛中。
            // 特征：包含 (Age: 且包含 ;ID:
            if (line.Contains("(Age:") && line.Contains(";ID:")) return true;
            
            return false;
        }
    }
}
