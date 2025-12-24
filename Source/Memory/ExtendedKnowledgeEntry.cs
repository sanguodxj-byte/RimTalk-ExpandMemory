using System;
using System.Collections.Generic;
using Verse;
using RimTalk.Memory;

namespace RimTalk.Memory
{
    /// <summary>
    /// 扩展的常识条目
    /// 添加两个新属性：
    /// - canBeExtracted: 是否允许被提取内容（用于常识链）
    /// - canBeMatched: 是否允许被匹配
    /// </summary>
    public static class ExtendedKnowledgeEntry
    {
        private static Dictionary<string, ExtendedProperties> extendedProps = new Dictionary<string, ExtendedProperties>();

        public class ExtendedProperties
        {
            public bool canBeExtracted = false;  // 默认允许被提取
            public bool canBeMatched = false;    // 默认允许被匹配
        }

        public static ExtendedProperties GetExtendedProperties(CommonKnowledgeEntry entry)
        {
            if (entry == null)
                return new ExtendedProperties();

            if (!extendedProps.ContainsKey(entry.id))
            {
                extendedProps[entry.id] = new ExtendedProperties();
            }

            return extendedProps[entry.id];
        }

        public static void SetCanBeExtracted(CommonKnowledgeEntry entry, bool value)
        {
            if (entry == null) return;
            GetExtendedProperties(entry).canBeExtracted = value;
        }

        public static void SetCanBeMatched(CommonKnowledgeEntry entry, bool value)
        {
            if (entry == null) return;
            GetExtendedProperties(entry).canBeMatched = value;
        }

        public static bool CanBeExtracted(CommonKnowledgeEntry entry)
        {
            if (entry == null) return false;
            return GetExtendedProperties(entry).canBeExtracted;
        }

        public static bool CanBeMatched(CommonKnowledgeEntry entry)
        {
            if (entry == null) return false;
            return GetExtendedProperties(entry).canBeMatched;
        }

        public static void CleanupDeletedEntries(CommonKnowledgeLibrary library)
        {
            if (library == null) return;

            var validIds = new HashSet<string>();
            foreach (var entry in library.Entries)
            {
                if (entry != null)
                    validIds.Add(entry.id);
            }

            var keysToRemove = new List<string>();
            foreach (var key in extendedProps.Keys)
            {
                if (!validIds.Contains(key))
                    keysToRemove.Add(key);
            }

            foreach (var key in keysToRemove)
            {
                extendedProps.Remove(key);
            }
        }

        public static void ExposeData()
        {
            List<string> keys = null;
            List<bool> canBeExtractedList = null;
            List<bool> canBeMatchedList = null;

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // 保存时：从字典中提取数据
                keys = new List<string>(extendedProps.Keys);
                canBeExtractedList = new List<bool>();
                canBeMatchedList = new List<bool>();

                foreach (var key in keys)
                {
                    canBeExtractedList.Add(extendedProps[key].canBeExtracted);
                    canBeMatchedList.Add(extendedProps[key].canBeMatched);
                }
            }

            // 序列化（保存和加载都会执行）
            Scribe_Collections.Look(ref keys, "extendedKnowledgeKeys", LookMode.Value);
            Scribe_Collections.Look(ref canBeExtractedList, "canBeExtractedList", LookMode.Value);
            Scribe_Collections.Look(ref canBeMatchedList, "canBeMatchedList", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // 加载时：恢复到字典中
                if (keys != null && canBeExtractedList != null && canBeMatchedList != null)
                {
                    extendedProps.Clear();
                    for (int i = 0; i < keys.Count && i < canBeExtractedList.Count && i < canBeMatchedList.Count; i++)
                    {
                        extendedProps[keys[i]] = new ExtendedProperties
                        {
                            canBeExtracted = canBeExtractedList[i],
                            canBeMatched = canBeMatchedList[i]
                        };
                    }
                    
                    Log.Message($"[RimTalk-ExpandMemory] Loaded extended properties for {keys.Count} knowledge entries");
                }
            }
        }
    }
}
