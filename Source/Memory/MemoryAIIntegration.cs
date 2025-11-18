using System.Text;
using System.Collections.Generic;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory
{
    /// <summary>
    /// Integration helper for AI conversation generation with memory context
    /// </summary>
    public static class MemoryAIIntegration
    {
        // 缓存已记录的对话，避免重复记录
        private static HashSet<string> recordedConversations = new HashSet<string>();
        private static int lastCleanupTick = 0;
        private const int CleanupInterval = 2500; // 清理间隔：约1小时游戏时间
        
        /// <summary>
        /// Generate AI prompt with pawn's memory context
        /// </summary>
        public static string GeneratePromptWithMemory(Pawn pawn, string basePrompt)
        {
            if (pawn == null)
                return basePrompt;

            var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
            if (memoryComp == null)
                return basePrompt;

            StringBuilder promptBuilder = new StringBuilder();
            
            // Add memory context
            string memoryContext = memoryComp.GetMemoryContext();
            if (!string.IsNullOrEmpty(memoryContext))
            {
                promptBuilder.AppendLine(memoryContext);
                promptBuilder.AppendLine();
            }

            // Add base prompt
            promptBuilder.Append(basePrompt);

            return promptBuilder.ToString();
        }

        /// <summary>
        /// Get a summary of pawn's current mental state based on memories
        /// </summary>
        public static string GetMentalStateSummary(Pawn pawn)
        {
            var memoryComp = pawn.TryGetComp<PawnMemoryComp>();
            if (memoryComp == null)
                return "";

            var recentMemories = memoryComp.GetRelevantMemories(3);
            if (recentMemories.Count == 0)
                return "";

            StringBuilder summary = new StringBuilder();
            summary.AppendLine("Recent experiences:");

            foreach (var memory in recentMemories)
            {
                string emotionalTag = GetEmotionalTag(memory);
                summary.AppendLine("- " + emotionalTag + memory.content);
            }

            return summary.ToString();
        }

        private static string GetEmotionalTag(MemoryEntry memory)
        {
            if (memory.importance > 0.8f)
                return "[Important] ";
            if (memory.importance < 0.3f)
                return "[Minor] ";
            return "";
        }

        /// <summary>
        /// Record AI-generated conversation as memory
        /// </summary>
        public static void RecordConversation(Pawn speaker, Pawn listener, string content)
        {
            // Check if conversation memory is enabled
            if (!RimTalkMemoryPatchMod.Settings.enableConversationMemory)
            {
                Log.Warning("[RimTalk Memory] ⚠️ Conversation memory is DISABLED in settings!");
                return;
            }
            
            // 清理旧的缓存（避免内存泄漏）
            if (Find.TickManager != null && Find.TickManager.TicksGame - lastCleanupTick > CleanupInterval)
            {
                recordedConversations.Clear();
                lastCleanupTick = Find.TickManager.TicksGame;
                if (Prefs.DevMode)
                    Log.Message("[RimTalk Memory] Cleaned conversation cache");
            }
            
            // 生成唯一ID（基于tick、参与者和内容hash）
            int tick = Find.TickManager?.TicksGame ?? 0;
            int contentHash = content?.GetHashCode() ?? 0;
            string conversationId = $"{tick}_{speaker?.ThingID}_{listener?.ThingID}_{contentHash}";
            
            // 如果已经记录过这次对话，跳过
            if (recordedConversations.Contains(conversationId))
            {
                if (Prefs.DevMode)
                    Log.Message($"[RimTalk Memory] ⏭️ Skipped duplicate in RecordConversation: {conversationId}");
                return;
            }
            
            // 标记为已记录
            recordedConversations.Add(conversationId);
                
            // Record for speaker（说话者视角）
            var speakerMemory = speaker != null ? speaker.TryGetComp<PawnMemoryComp>() : null;
            if (speakerMemory != null)
            {
                string listenerName = listener != null ? listener.LabelShort : "self";
                string memoryContent = "Said to " + listenerName + ": " + content;
                speakerMemory.AddMemory(memoryContent, MemoryType.Conversation, 0.6f, listenerName);
            }

            // Record for listener（听者视角）- 只在listener存在且不是自己时记录
            if (listener != null && listener != speaker)
            {
                var listenerMemory = listener.TryGetComp<PawnMemoryComp>();
                if (listenerMemory != null)
                {
                    string speakerName = speaker != null ? speaker.LabelShort : "someone";
                    string memoryContent = speakerName + " said: " + content;
                    listenerMemory.AddMemory(memoryContent, MemoryType.Conversation, 0.5f, speakerName);
                }
            }
            
            // 统一的日志输出（成功）
            string speakerLabel = speaker != null ? speaker.LabelShort : "Unknown";
            string listenerLabel = listener != null && listener != speaker ? listener.LabelShort : "self";
            string previewContent = content.Length > 50 ? content.Substring(0, 50) + "..." : content;
            Log.Message($"[RimTalk Memory] ✅ RECORDED: {speakerLabel} -> {listenerLabel}: {previewContent}");
        }
    }
}
