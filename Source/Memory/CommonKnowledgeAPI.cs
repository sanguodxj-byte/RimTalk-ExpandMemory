using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// 常识库公共 API - 供其他 Mod 使用
    /// 
    /// 使用方法：
    /// 1. 添加常识：CommonKnowledgeAPI.AddKnowledge("标签", "内容", 0.5f)
    /// 2. 更新常识：CommonKnowledgeAPI.UpdateKnowledge("id", "新内容")
    /// 3. 查询常识：CommonKnowledgeAPI.FindKnowledge("标签")
    /// 4. 删除常识：CommonKnowledgeAPI.RemoveKnowledge("id")
    /// 
    /// 版本：v3.3.x
    /// </summary>
    public static class CommonKnowledgeAPI
    {
        #region 添加常识

        /// <summary>
        /// 添加一条常识（简化版）
        /// </summary>
        /// <param name="tag">标签，支持多个（用逗号分隔）</param>
        /// <param name="content">内容</param>
        /// <param name="importance">重要性（0-1），默认0.5</param>
        /// <returns>新常识的ID</returns>
        public static string AddKnowledge(string tag, string content, float importance = 0.5f)
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                {
                    Log.Warning("[CommonKnowledgeAPI] Failed to get library");
                    return null;
                }

                var entry = new CommonKnowledgeEntry(tag, content)
                {
                    importance = UnityEngine.Mathf.Clamp01(importance),
                    isEnabled = true,
                    isUserEdited = false // 标记为非用户编辑，可以被自动处理
                };

                library.AddEntry(entry);
                
                return entry.id;
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] AddKnowledge failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 添加一条常识（完整版）
        /// </summary>
        /// <param name="tag">标签，支持多个（用逗号分隔）</param>
        /// <param name="content">内容</param>
        /// <param name="importance">重要性（0-1）</param>
        /// <param name="matchMode">匹配模式（Any=任意一个标签匹配即可，All=所有标签必须匹配）</param>
        /// <param name="targetPawnId">目标Pawn ID（-1=全局，其他=仅对该Pawn有效）</param>
        /// <param name="canBeExtracted">是否可以被提取（用于常识链）</param>
        /// <param name="canBeMatched">是否可以被匹配（用于常识链）</param>
        /// <returns>新常识的ID</returns>
        public static string AddKnowledgeEx(
            string tag, 
            string content, 
            float importance = 0.5f,
            KeywordMatchMode matchMode = KeywordMatchMode.Any,
            int targetPawnId = -1,
            bool canBeExtracted = false,
            bool canBeMatched = false)
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                {
                    Log.Warning("[CommonKnowledgeAPI] Failed to get library");
                    return null;
                }

                var entry = new CommonKnowledgeEntry(tag, content)
                {
                    importance = UnityEngine.Mathf.Clamp01(importance),
                    matchMode = matchMode,
                    targetPawnId = targetPawnId,
                    isEnabled = true,
                    isUserEdited = false
                };

                // 设置扩展属性
                ExtendedKnowledgeEntry.SetCanBeExtracted(entry, canBeExtracted);
                ExtendedKnowledgeEntry.SetCanBeMatched(entry, canBeMatched);

                library.AddEntry(entry);
                
                return entry.id;
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] AddKnowledgeEx failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 批量添加常识
        /// </summary>
        /// <param name="knowledgeList">常识列表（标签-内容对）</param>
        /// <param name="importance">默认重要性</param>
        /// <returns>成功添加的数量</returns>
        public static int AddKnowledgeBatch(List<(string tag, string content)> knowledgeList, float importance = 0.5f)
        {
            if (knowledgeList == null || knowledgeList.Count == 0)
                return 0;

            int count = 0;
            foreach (var (tag, content) in knowledgeList)
            {
                if (AddKnowledge(tag, content, importance) != null)
                    count++;
            }

            return count;
        }

        #endregion

        #region 更新常识

        /// <summary>
        /// 更新常识内容
        /// </summary>
        /// <param name="id">常识ID</param>
        /// <param name="newContent">新内容</param>
        /// <returns>是否成功</returns>
        public static bool UpdateKnowledge(string id, string newContent)
        {
            try
            {
                var entry = FindKnowledgeById(id);
                if (entry == null)
                {
                    Log.Warning($"[CommonKnowledgeAPI] Knowledge not found: {id}");
                    return false;
                }

                entry.content = newContent;
                entry.InvalidateCache();
                
                // 触发向量更新（如果启用）
                try
                {
                    if (MemoryPatch.RimTalkMemoryPatchMod.Settings.enableVectorEnhancement)
                    {
                        VectorDB.VectorService.Instance.UpdateKnowledgeVector(entry.id, entry.content);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[CommonKnowledgeAPI] Failed to update vector: {ex.Message}");
                }

                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] UpdateKnowledge failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 更新常识标签
        /// </summary>
        /// <param name="id">常识ID</param>
        /// <param name="newTag">新标签</param>
        /// <returns>是否成功</returns>
        public static bool UpdateKnowledgeTag(string id, string newTag)
        {
            try
            {
                var entry = FindKnowledgeById(id);
                if (entry == null)
                    return false;

                entry.tag = newTag;
                entry.InvalidateCache();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] UpdateKnowledgeTag failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 更新常识重要性
        /// </summary>
        /// <param name="id">常识ID</param>
        /// <param name="newImportance">新重要性（0-1）</param>
        /// <returns>是否成功</returns>
        public static bool UpdateKnowledgeImportance(string id, float newImportance)
        {
            try
            {
                var entry = FindKnowledgeById(id);
                if (entry == null)
                    return false;

                entry.importance = UnityEngine.Mathf.Clamp01(newImportance);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] UpdateKnowledgeImportance failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 启用/禁用常识
        /// </summary>
        /// <param name="id">常识ID</param>
        /// <param name="enabled">是否启用</param>
        /// <returns>是否成功</returns>
        public static bool SetKnowledgeEnabled(string id, bool enabled)
        {
            try
            {
                var entry = FindKnowledgeById(id);
                if (entry == null)
                    return false;

                entry.isEnabled = enabled;
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] SetKnowledgeEnabled failed: {ex}");
                return false;
            }
        }

        #endregion

        #region 查询常识

        /// <summary>
        /// 根据ID查找常识
        /// </summary>
        /// <param name="id">常识ID</param>
        /// <returns>常识条目，未找到返回null</returns>
        public static CommonKnowledgeEntry FindKnowledgeById(string id)
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return null;

                return library.Entries.FirstOrDefault(e => e.id == id);
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] FindKnowledgeById failed: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 根据标签查找常识（支持部分匹配）
        /// </summary>
        /// <param name="tag">标签（支持部分匹配）</param>
        /// <returns>匹配的常识列表</returns>
        public static List<CommonKnowledgeEntry> FindKnowledge(string tag)
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return new List<CommonKnowledgeEntry>();

                return library.Entries
                    .Where(e => e.tag.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] FindKnowledge failed: {ex}");
                return new List<CommonKnowledgeEntry>();
            }
        }

        /// <summary>
        /// 根据内容查找常识（支持部分匹配）
        /// </summary>
        /// <param name="content">内容关键词</param>
        /// <returns>匹配的常识列表</returns>
        public static List<CommonKnowledgeEntry> FindKnowledgeByContent(string content)
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return new List<CommonKnowledgeEntry>();

                return library.Entries
                    .Where(e => e.content.IndexOf(content, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] FindKnowledgeByContent failed: {ex}");
                return new List<CommonKnowledgeEntry>();
            }
        }

        /// <summary>
        /// 获取所有常识
        /// </summary>
        /// <returns>所有常识列表</returns>
        public static List<CommonKnowledgeEntry> GetAllKnowledge()
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return new List<CommonKnowledgeEntry>();

                return library.Entries.ToList();
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] GetAllKnowledge failed: {ex}");
                return new List<CommonKnowledgeEntry>();
            }
        }

        /// <summary>
        /// 获取常识数量
        /// </summary>
        /// <returns>常识总数</returns>
        public static int GetKnowledgeCount()
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return 0;

                return library.Entries.Count;
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] GetKnowledgeCount failed: {ex}");
                return 0;
            }
        }

        #endregion

        #region 删除常识

        /// <summary>
        /// 根据ID删除常识
        /// </summary>
        /// <param name="id">常识ID</param>
        /// <returns>是否成功</returns>
        public static bool RemoveKnowledge(string id)
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return false;

                var entry = library.Entries.FirstOrDefault(e => e.id == id);
                if (entry == null)
                    return false;

                library.RemoveEntry(entry);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] RemoveKnowledge failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// 根据标签删除所有匹配的常识
        /// </summary>
        /// <param name="tag">标签</param>
        /// <returns>删除的数量</returns>
        public static int RemoveKnowledgeByTag(string tag)
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return 0;

                var toRemove = library.Entries
                    .Where(e => e.tag.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                int count = 0;
                foreach (var entry in toRemove)
                {
                    library.RemoveEntry(entry);
                    count++;
                }

                return count;
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] RemoveKnowledgeByTag failed: {ex}");
                return 0;
            }
        }

        /// <summary>
        /// 清空所有常识（危险操作）
        /// </summary>
        /// <returns>是否成功</returns>
        public static bool ClearAllKnowledge()
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return false;

                library.Clear();
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] ClearAllKnowledge failed: {ex}");
                return false;
            }
        }

        #endregion

        #region 导入/导出

        /// <summary>
        /// 从文本导入常识
        /// </summary>
        /// <param name="text">格式化的常识文本（每行一条）</param>
        /// <param name="clearExisting">是否清空现有常识</param>
        /// <returns>导入的数量</returns>
        public static int ImportFromText(string text, bool clearExisting = false)
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return 0;

                return library.ImportFromText(text, clearExisting);
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] ImportFromText failed: {ex}");
                return 0;
            }
        }

        /// <summary>
        /// 导出所有常识为文本
        /// </summary>
        /// <returns>格式化的常识文本</returns>
        public static string ExportToText()
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return string.Empty;

                return library.ExportToText();
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] ExportToText failed: {ex}");
                return string.Empty;
            }
        }

        #endregion

        #region 高级功能

        /// <summary>
        /// 检查常识是否存在（根据ID）
        /// </summary>
        /// <param name="id">常识ID</param>
        /// <returns>是否存在</returns>
        public static bool ExistsKnowledge(string id)
        {
            return FindKnowledgeById(id) != null;
        }

        /// <summary>
        /// 获取常识库统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public static KnowledgeStats GetStats()
        {
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return new KnowledgeStats();

                return new KnowledgeStats
                {
                    TotalCount = library.Entries.Count,
                    EnabledCount = library.Entries.Count(e => e.isEnabled),
                    DisabledCount = library.Entries.Count(e => !e.isEnabled),
                    UserEditedCount = library.Entries.Count(e => e.isUserEdited),
                    GlobalCount = library.Entries.Count(e => e.targetPawnId == -1),
                    PawnSpecificCount = library.Entries.Count(e => e.targetPawnId != -1)
                };
            }
            catch (Exception ex)
            {
                Log.Error($"[CommonKnowledgeAPI] GetStats failed: {ex}");
                return new KnowledgeStats();
            }
        }

        #endregion
    }

    /// <summary>
    /// 常识库统计信息
    /// </summary>
    public struct KnowledgeStats
    {
        public int TotalCount;          // 总数
        public int EnabledCount;        // 启用的数量
        public int DisabledCount;       // 禁用的数量
        public int UserEditedCount;     // 用户编辑的数量
        public int GlobalCount;         // 全局常识数量
        public int PawnSpecificCount;   // Pawn专属常识数量
    }
}
