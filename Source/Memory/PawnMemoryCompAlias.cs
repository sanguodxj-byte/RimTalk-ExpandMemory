using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 兼容层：将旧的 PawnMemoryComp API 映射到新的 FourLayerMemoryComp
    /// 这允许旧代码无缝切换到新系统
    /// </summary>
    public class PawnMemoryComp : FourLayerMemoryComp
    {
        // 兼容旧API：ShortTermMemories（映射到 SCM）
        public List<MemoryEntry> ShortTermMemories => SituationalMemories;

        // 兼容旧API：LongTermMemories（映射到 ELS + CLPA）
        public List<MemoryEntry> LongTermMemories
        {
            get
            {
                var combined = new List<MemoryEntry>();
                combined.AddRange(EventLogMemories);
                combined.AddRange(ArchiveMemories);
                return combined;
            }
        }

        // 兼容旧API：AddMemory（映射到 AddActiveMemory）
        public void AddMemory(string content, MemoryType type, float importance = 1f, string relatedPawn = null)
        {
            AddActiveMemory(content, type, importance, relatedPawn);
        }

        // 兼容旧API：DecayMemories（映射到 DecayActivity）
        public void DecayMemories()
        {
            DecayActivity();
        }

        // 兼容旧API：GetMemoryContext
        public string GetMemoryContext(int count = 5)
        {
            var query = new MemoryQuery
            {
                maxCount = count,
                includeContext = true
            };

            var memories = RetrieveMemories(query);
            var context = new System.Text.StringBuilder();

            foreach (var memory in memories)
            {
                context.AppendLine($"- [{memory.TypeName}] {memory.content} ({memory.TimeAgoString})");
            }

            return context.ToString();
        }

        // 兼容旧API：GetRelevantMemories
        public List<MemoryEntry> GetRelevantMemories(int count = 5)
        {
            var query = new MemoryQuery
            {
                maxCount = count,
                includeContext = true
            };

            return RetrieveMemories(query);
        }

        // 兼容旧API：ClearAllMemories
        public void ClearAllMemories()
        {
            ActiveMemories.Clear();
            SituationalMemories.Clear();
            EventLogMemories.Clear();
            // 不清除 Archive（长期记忆应该保留）
        }

        // 兼容旧API：ClearShortTermMemories
        public void ClearShortTermMemories()
        {
            ActiveMemories.Clear();
            SituationalMemories.Clear();
        }

        // 兼容旧API：ClearLongTermMemories
        public void ClearLongTermMemories()
        {
            EventLogMemories.Clear();
            // 不清除 Archive
        }
        
        // 新增：获取SCM记忆数量（用于UI判断）
        public int GetSituationalMemoryCount()
        {
            return SituationalMemories.Count;
        }
    }
}
