using RimTalk.API;
using RimTalk.MemoryPatch;
using RimTalk.Prompt;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace RimTalk.Memory.API
{
    /// <summary>
    /// RimTalk 新 API 集成入口
    /// 使用官方 API 注册变量和 PromptEntry，替代 Harmony Patch
    ///
    /// 功能：
    /// 1. 注册 {{pawn.memory}} Pawn 变量 - 提供角色记忆
    /// 2. 注册 {{knowledge}} Context 变量 - 提供匹配的常识
    /// 3. 添加 PromptEntry - 在末尾注入记忆和常识上下文
    ///
    /// ⭐ v5.0: 适配 RimTalk 新版 Scriban 模板系统
    /// - MustacheContext → PromptContext
    /// - MustacheParser → ScribanParser
    /// </summary>
    [StaticConstructorOnStartup]
    public static class RimTalkAPIIntegration
    {
        // ⭐ v4.0+: 使用标准化的 ModId 格式
        private const string MOD_ID = "RimTalk.MemoryPatch";
        private const string ENTRY_NAME = "Memory & Knowledge Context";
        // ⭐ 不再手动设置 ID - 新 API 会自动根据 SourceModId + Name 生成确定性 ID
        // 格式: mod_{sanitized_mod_id}_{sanitized_name}
        
        private static bool _initialized = false;
        private static bool _apiAvailable = false;
        
        // 缓存的 RimTalk 程序集和类型
        private static Assembly _rimTalkAssembly;
        private static Type _promptAPIType;
        private static Type _promptEntryType;
        private static Type _promptRoleType;
        private static Type _promptPositionType;
        private static Type _promptContextType;  // ⭐ v5.0: 改名为 PromptContext
        
        // 用于嗅探预设是否已自定义记忆变量的关键词
        private static readonly string[] SniffingKeywords = new string[]
        {
            "pawn.memory", "p.memory",
            "pawn.ABM", "p.ABM",
            "pawn.ELS", "p.ELS",
            "pawn.CLPA", "p.CLPA",
            "{{knowledge}}", "{{ knowledge }}",
            "knowledge_grouped", "knowledge_rules", 
            "knowledge_lore", "knowledge_status", 
            "knowledge_history"
        };
        
        // 标志：是否使用新 API（用于禁用旧 Patch）
        public static bool IsUsingNewAPI => _apiAvailable;
        
        static RimTalkAPIIntegration()
        {
            // 延迟初始化，确保 RimTalk 已加载
            LongEventHandler.ExecuteWhenFinished(Initialize);
        }
        
        private static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            
            try
            {
                // 检测新 API 是否可用
                if (!DetectNewAPI())
                {
                    Log.Message("[MemoryPatch] New RimTalk API not detected, using legacy Harmony patches");
                    return;
                }
                
                _apiAvailable = true;
                
                // 注册变量
                RegisterVariables();
                
                // 注册 PromptEntry
                RegisterPromptEntry();
                
                Log.Message("[MemoryPatch] ✓ Integrated via RimTalk API v4.0+");
                Log.Message("[MemoryPatch]   - Registered {{pawn.memory}} variable (combined memories)");
                Log.Message("[MemoryPatch]   - Registered {{pawn.ABM}} variable (active buffer - raw)");
                Log.Message("[MemoryPatch]   - Registered {{pawn.ELS}} variable (event log - raw)");
                Log.Message("[MemoryPatch]   - Registered {{pawn.CLPA}} variable (archive - raw)");
                Log.Message("[MemoryPatch]   - Registered {{pawn.matchELS}} variable (event log - matched)");
                Log.Message("[MemoryPatch]   - Registered {{pawn.matchCLPA}} variable (archive - matched)");
                Log.Message("[MemoryPatch]   - Registered {{knowledge}} variable");
                Log.Message("[MemoryPatch]   - Added PromptEntry: " + ENTRY_NAME);
            }
            catch (Exception ex)
            {
                Log.Error($"[MemoryPatch] API integration failed: {ex.Message}");
                Log.Message("[MemoryPatch] Falling back to legacy Harmony patches");
                _apiAvailable = false;
            }
        }
        
        /// <summary>
        /// 检测新 API 是否可用
        /// </summary>
        private static bool DetectNewAPI()
        {
            _rimTalkAssembly = GetRimTalkAssembly();
            if (_rimTalkAssembly == null) 
            {
                Log.Warning("[MemoryPatch] RimTalk assembly not found");
                return false;
            }
            
            // 检测关键类型
            _promptAPIType = _rimTalkAssembly.GetType("RimTalk.API.RimTalkPromptAPI");
            _promptEntryType = _rimTalkAssembly.GetType("RimTalk.Prompt.PromptEntry");
            _promptRoleType = _rimTalkAssembly.GetType("RimTalk.Prompt.PromptRole");
            _promptPositionType = _rimTalkAssembly.GetType("RimTalk.Prompt.PromptPosition");
            // ⭐ v5.0: 查找 PromptContext（新版）或 MustacheContext（旧版兼容）
            _promptContextType = _rimTalkAssembly.GetType("RimTalk.Prompt.PromptContext")
                ?? _rimTalkAssembly.GetType("RimTalk.Prompt.MustacheContext");
            
            // 检查 API 类是否存在
            if (_promptAPIType == null)
            {
                Log.Message("[MemoryPatch] RimTalk.API.RimTalkPromptAPI not found - old RimTalk version");
                return false;
            }
            
            // 检查关键方法
            var registerPawnVar = _promptAPIType.GetMethod("RegisterPawnVariable");
            var registerCtxVar = _promptAPIType.GetMethod("RegisterContextVariable");
            var addEntry = _promptAPIType.GetMethod("AddPromptEntry");
            
            if (registerPawnVar == null || registerCtxVar == null || addEntry == null)
            {
                Log.Warning("[MemoryPatch] RimTalk API methods not found");
                return false;
            }
            
            Log.Message("[MemoryPatch] Detected RimTalk API v4.0+ with Scriban/Mustache support");
            return true;
        }
        
        /// <summary>
        /// 获取 RimTalk 程序集
        /// </summary>
        private static Assembly GetRimTalkAssembly()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "RimTalk")
                {
                    return assembly;
                }
            }
            return null;
        }
        
        /// <summary>
        /// 注册 Mustache 变量
        /// </summary>
        private static void RegisterVariables()
        {
            // 1. 注册 {{pawn.memory}} - Pawn 变量（综合记忆）
            // 使用 Func<Pawn, string> 委托
            var registerPawnVar = _promptAPIType.GetMethod("RegisterPawnVariable");
            if (registerPawnVar != null)
            {
                try
                {
                    // 创建委托
                    Func<Pawn, string> memoryProvider = MemoryVariableProvider.GetPawnMemory;
                    
                    // 调用 RegisterPawnVariable(modId, variableName, provider, description, priority)
                    registerPawnVar.Invoke(null, new object[]
                    {
                        MOD_ID,
                        "memory",
                        memoryProvider,
                        "Character's personal memories and experiences",
                        100 // priority
                    });
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message("[MemoryPatch] ✓ Registered {{pawn.memory}} variable");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[MemoryPatch] Failed to register pawn.memory: {ex.Message}");
                }
                
                // 2. 注册 {{pawn.ABM}} - ABM 层记忆（超短期记忆）
                try
                {
                    Func<Pawn, string> abmProvider = MemoryVariableProvider.GetPawnABM;
                    
                    registerPawnVar.Invoke(null, new object[]
                    {
                        MOD_ID,
                        "ABM",
                        abmProvider,
                        "Character's active buffer memories (recent conversations)",
                        100
                    });
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message("[MemoryPatch] ✓ Registered {{pawn.ABM}} variable");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[MemoryPatch] Failed to register pawn.ABM: {ex.Message}");
                }
                
                // 3. 注册 {{pawn.ELS}} - ELS 层记忆（中期记忆）
                try
                {
                    Func<Pawn, string> elsProvider = MemoryVariableProvider.GetPawnELS;
                    
                    registerPawnVar.Invoke(null, new object[]
                    {
                        MOD_ID,
                        "ELS",
                        elsProvider,
                        "Character's event log summary (mid-term memories)",
                        100
                    });
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message("[MemoryPatch] ✓ Registered {{pawn.ELS}} variable");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[MemoryPatch] Failed to register pawn.ELS: {ex.Message}");
                }
                
                // 4. 注册 {{pawn.CLPA}} - CLPA 层记忆（长期记忆）
                try
                {
                    Func<Pawn, string> clpaProvider = MemoryVariableProvider.GetPawnCLPA;
                    
                    registerPawnVar.Invoke(null, new object[]
                    {
                        MOD_ID,
                        "CLPA",
                        clpaProvider,
                        "Character's persona archive (long-term memories)",
                        100
                    });
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message("[MemoryPatch] ✓ Registered {{pawn.CLPA}} variable");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[MemoryPatch] Failed to register pawn.CLPA: {ex.Message}");
                }
                
                // 5. 注册 {{pawn.matchELS}} - 匹配后的 ELS 层记忆
                try
                {
                    Func<Pawn, string> matchElsProvider = MemoryVariableProvider.GetPawnMatchELS;
                    
                    registerPawnVar.Invoke(null, new object[]
                    {
                        MOD_ID,
                        "matchELS",
                        matchElsProvider,
                        "Context-matched event log memories (mid-term)",
                        100
                    });
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message("[MemoryPatch] ✓ Registered {{pawn.matchELS}} variable");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[MemoryPatch] Failed to register pawn.matchELS: {ex.Message}");
                }
                
                // 6. 注册 {{pawn.matchCLPA}} - 匹配后的 CLPA 层记忆
                try
                {
                    Func<Pawn, string> matchClpaProvider = MemoryVariableProvider.GetPawnMatchCLPA;
                    
                    registerPawnVar.Invoke(null, new object[]
                    {
                        MOD_ID,
                        "matchCLPA",
                        matchClpaProvider,
                        "Context-matched archive memories (long-term)",
                        100
                    });
                    
                    if (Prefs.DevMode)
                    {
                        Log.Message("[MemoryPatch] ✓ Registered {{pawn.matchCLPA}} variable");
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning($"[MemoryPatch] Failed to register pawn.matchCLPA: {ex.Message}");
                }
            }
            
        
            // 7. 注册 {{knowledge}} - Context 变量（保持原有格式不变）
            var registerCtxVar = _promptAPIType.GetMethod("RegisterContextVariable");
            if (registerCtxVar != null)
            {
                // 原有变量 {{knowledge}} — 保持 "1. [tag] content" 格式，兼容旧预设
                RegisterOneContextVariable(registerCtxVar, "knowledge",
                    new Func<object, string>(KnowledgeVariableProvider.GetMatchedKnowledge),
                    "World knowledge matched to conversation context", 100);
                
                // 新增：按分类分组的完整输出
                RegisterOneContextVariable(registerCtxVar, "knowledge_grouped",
                    new Func<object, string>(KnowledgeVariableProvider.GetGroupedKnowledge),
                    "World knowledge grouped by category headers", 101);
                
                // 新增：按分类拆分的独立变量
                RegisterOneContextVariable(registerCtxVar, "knowledge_rules",
                    new Func<object, string>(KnowledgeVariableProvider.GetKnowledgeRules),
                    "Knowledge: rules and instructions", 102);
                
                RegisterOneContextVariable(registerCtxVar, "knowledge_lore",
                    new Func<object, string>(KnowledgeVariableProvider.GetKnowledgeLore),
                    "Knowledge: world lore and background", 103);
                
                RegisterOneContextVariable(registerCtxVar, "knowledge_status",
                    new Func<object, string>(KnowledgeVariableProvider.GetKnowledgeStatus),
                    "Knowledge: pawn/colonist status", 104);
                
                RegisterOneContextVariable(registerCtxVar, "knowledge_history",
                    new Func<object, string>(KnowledgeVariableProvider.GetKnowledgeHistory),
                    "Knowledge: historical events", 105);
                
                RegisterOneContextVariable(registerCtxVar, "knowledge_other",
                    new Func<object, string>(KnowledgeVariableProvider.GetKnowledgeOther),
                    "Knowledge: uncategorized", 106);
            }
        }
        
        /// <summary>
        /// 注册单个 Context 变量的辅助方法
        /// </summary>
        private static void RegisterOneContextVariable(MethodInfo registerMethod, string name, Func<object, string> provider, string description, int priority)
        {
            try
            {
                registerMethod.Invoke(null, new object[] { MOD_ID, name, provider, description, priority });
                if (Prefs.DevMode)
                {
                    Log.Message($"[MemoryPatch] ✓ Registered {{{{{name}}}}} variable");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MemoryPatch] Failed to register {name}: {ex.Message}");
            }
        }

        // Chat History 条目的名称
        private const string CHAT_HISTORY_ENTRY_NAME = "Chat History";
        
        /// <summary>
        /// 注册 PromptEntry
        /// ⭐ v4.0+: 使用新的确定性 ID 机制
        /// - 不再手动设置 Id
        /// - 必须先设置 SourceModId 再设置 Name（因为 Name setter 会触发 ID 更新）
        /// - ID 会自动生成为 mod_{sanitized_mod_id}_{sanitized_name}
        /// ⭐ v4.1: 支持更新现有条目内容，无需重置默认
        /// ⭐ v5.0: 在 Chat History 条目后插入，并自动禁用 Chat History
        /// </summary>
        private static void RegisterPromptEntry()
        {
            try
            {
                // ⭐ v4.1: 首先检查是否已存在该条目，如果存在则更新内容
                string entryId = GetDeterministicId(MOD_ID, ENTRY_NAME);
                
                // 获取 ActivePreset
                var getPresetMethod = _promptAPIType.GetMethod("GetActivePreset");
                object preset = null;
                if (getPresetMethod != null)
                {
                    preset = getPresetMethod.Invoke(null, null);
                    if (preset != null)
                    {
                        // 尝试获取现有条目
                        var getEntryMethod = preset.GetType().GetMethod("GetEntry");
                        if (getEntryMethod != null)
                        {
                            var existingEntry = getEntryMethod.Invoke(preset, new object[] { entryId });
                            if (existingEntry != null)
                            {
                                // ⭐ 条目已存在 → 直接更新 Content
                                SetProperty(existingEntry, "Content", GetMemoryEntryContent());
                                Log.Message($"[MemoryPatch] ✓ Updated existing PromptEntry: {ENTRY_NAME}");
                                
                                // ⭐ v5.0: 仍然检查并禁用 Chat History
                                DisableChatHistoryIfEnabled(preset);
                                return;
                            }
                        }
                        
                        // ⭐ 新增：内容特征嗅探 (Content Heuristic Sniffing)
                        // 如果我们在预设中没有找到当前 Mod 注入的 Entry，
                        // 我们扫描现有所有 Entry 的内容，判断预设是否已自行处理记忆上下文
                        if (preset is PromptPreset rimtalkPreset)
                        {
                            if (rimtalkPreset.Entries != null)
                            {
                                foreach (var currentEntry in rimtalkPreset.Entries)
                                {
                                    string content = currentEntry.Content;
                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        foreach (var keyword in SniffingKeywords)
                                        {
                                            if (content.Contains(keyword))
                                            {
                                                Log.Message($"[MemoryPatch] Detected custom memory variable '{keyword}' in active preset. Skipping auto-injection.");
                                                return;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                
                // 条目不存在，创建新的 PromptEntry 实例
                var entry = Activator.CreateInstance(_promptEntryType);
                if (entry == null)
                {
                    Log.Warning("[MemoryPatch] Failed to create PromptEntry instance");
                    return;
                }
                
                // ⭐ 关键：必须先设置 SourceModId 再设置 Name
                // 因为 Name setter 会检查 SourceModId 并自动生成确定性 ID
                // 格式: mod_{sanitized_mod_id}_{sanitized_name}
                SetProperty(entry, "SourceModId", MOD_ID);  // 先设置 SourceModId
                SetProperty(entry, "Name", ENTRY_NAME);     // 再设置 Name（触发 ID 更新）
                SetProperty(entry, "Content", GetMemoryEntryContent());
                SetProperty(entry, "Enabled", true);
                // ⭐ 不再设置 Id - 让 PromptEntry 自动生成
                
                // 设置 Role = System
                if (_promptRoleType != null)
                {
                    var systemRole = Enum.Parse(_promptRoleType, "System");
                    SetProperty(entry, "Role", systemRole);
                }
                
                // 设置 Position = Relative
                if (_promptPositionType != null)
                {
                    var relativePos = Enum.Parse(_promptPositionType, "Relative");
                    SetProperty(entry, "Position", relativePos);
                }
                
                // ⭐ v5.0: 在 Chat History 条目后插入（而不是添加到末尾）
                var insertAfterNameMethod = _promptAPIType.GetMethod("InsertPromptEntryAfterName");
                if (insertAfterNameMethod != null)
                {
                    var result = insertAfterNameMethod.Invoke(null, new object[] { entry, CHAT_HISTORY_ENTRY_NAME });
                    if (result is bool success)
                    {
                        if (success)
                        {
                            Log.Message($"[MemoryPatch] ✓ Inserted PromptEntry after '{CHAT_HISTORY_ENTRY_NAME}': {ENTRY_NAME}");
                        }
                        else
                        {
                            // Chat History 未找到，已添加到末尾
                            Log.Message($"[MemoryPatch] ✓ Added PromptEntry at end ('{CHAT_HISTORY_ENTRY_NAME}' not found): {ENTRY_NAME}");
                        }
                        
                        // ⭐ v5.0: 禁用 Chat History
                        if (preset != null)
                        {
                            DisableChatHistoryIfEnabled(preset);
                        }
                    }
                }
                else
                {
                    // 回退：使用旧的 AddPromptEntry 方法
                    var addMethod = _promptAPIType.GetMethod("AddPromptEntry");
                    if (addMethod != null)
                    {
                        var result = addMethod.Invoke(null, new[] { entry });
                        if (result is bool success && success)
                        {
                            Log.Message($"[MemoryPatch] ✓ Added PromptEntry: {ENTRY_NAME}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MemoryPatch] Failed to register PromptEntry: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ⭐ v5.0: 检查并禁用 Chat History 条目（如果它是开启的）
        /// 这样可以避免与我们的记忆系统冲突
        /// </summary>
        private static void DisableChatHistoryIfEnabled(object preset)
        {
            try
            {
                if (preset == null) return;
                
                // 查找 Chat History 条目
                var findEntryByNameMethod = preset.GetType().GetMethod("FindEntryIdByName");
                if (findEntryByNameMethod == null)
                {
                    if (Prefs.DevMode) Log.Warning("[MemoryPatch] FindEntryIdByName method not found");
                    return;
                }
                
                var chatHistoryId = findEntryByNameMethod.Invoke(preset, new object[] { CHAT_HISTORY_ENTRY_NAME }) as string;
                if (string.IsNullOrEmpty(chatHistoryId))
                {
                    if (Prefs.DevMode) Log.Message($"[MemoryPatch] '{CHAT_HISTORY_ENTRY_NAME}' entry not found in preset");
                    return;
                }
                
                var getEntryMethod = preset.GetType().GetMethod("GetEntry");
                if (getEntryMethod == null)
                {
                    if (Prefs.DevMode) Log.Warning("[MemoryPatch] GetEntry method not found");
                    return;
                }
                
                var chatHistoryEntry = getEntryMethod.Invoke(preset, new object[] { chatHistoryId });
                if (chatHistoryEntry == null)
                {
                    if (Prefs.DevMode) Log.Warning("[MemoryPatch] Chat History entry is null");
                    return;
                }
                
                // ⭐ 修复：Enabled 是字段（field），不是属性（property）
                // 先尝试属性，如果没有则尝试字段
                var enabledProp = chatHistoryEntry.GetType().GetProperty("Enabled");
                var enabledField = chatHistoryEntry.GetType().GetField("Enabled");
                
                bool isEnabled = false;
                if (enabledProp != null)
                {
                    isEnabled = (bool)enabledProp.GetValue(chatHistoryEntry);
                }
                else if (enabledField != null)
                {
                    isEnabled = (bool)enabledField.GetValue(chatHistoryEntry);
                }
                else
                {
                    if (Prefs.DevMode) Log.Warning("[MemoryPatch] Enabled property/field not found on PromptEntry");
                    return;
                }
                
                if (isEnabled)
                {
                    // 禁用 Chat History
                    if (enabledProp != null)
                    {
                        enabledProp.SetValue(chatHistoryEntry, false);
                    }
                    else if (enabledField != null)
                    {
                        enabledField.SetValue(chatHistoryEntry, false);
                    }
                    Log.Message($"[MemoryPatch] ✓ Disabled '{CHAT_HISTORY_ENTRY_NAME}' to avoid conflict with Memory & Knowledge injection");
                }
                else
                {
                    if (Prefs.DevMode) Log.Message($"[MemoryPatch] '{CHAT_HISTORY_ENTRY_NAME}' is already disabled");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[MemoryPatch] Failed to disable Chat History: {ex.Message}");
                if (Prefs.DevMode)
                {
                    Log.Warning($"[MemoryPatch] Stack trace: {ex.StackTrace}");
                }
            }
        }
        
        /// <summary>
        /// 获取 Memory Entry 的模板内容
        /// </summary>
        private static string GetMemoryEntryContent()
        {
            return @"---
# Memory Context
{{-for p in pawns }}
## {{ p.name }}'s Memories:
{{ p.memory }}
{{- end }}

# World Knowledge:
{{knowledge}}
---";
        }
        
        /// <summary>
        /// 设置对象属性
        /// </summary>
        private static void SetProperty(object obj, string propertyName, object value)
        {
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(obj, value);
            }
            else
            {
                // 尝试字段
                var field = obj.GetType().GetField(propertyName);
                if (field != null)
                {
                    field.SetValue(obj, value);
                }
            }
        }
        
        /// <summary>
        /// 清理：在 Mod 卸载时调用
        /// ⭐ v4.0+: 使用确定性 ID 格式
        /// </summary>
        public static void Cleanup()
        {
            if (!_apiAvailable) return;
            
            try
            {
                // 计算确定性 ID
                string entryId = GetDeterministicId(MOD_ID, ENTRY_NAME);
                
                // 移除 PromptEntry
                var removeMethod = _promptAPIType?.GetMethod("RemovePromptEntry");
                if (removeMethod != null)
                {
                    removeMethod.Invoke(null, new object[] { entryId });
                    Log.Message($"[MemoryPatch] Removed PromptEntry: {entryId}");
                }
                
                // 注销所有 Hooks
                var unregisterMethod = _promptAPIType?.GetMethod("UnregisterAllHooks");
                if (unregisterMethod != null)
                {
                    unregisterMethod.Invoke(null, new object[] { MOD_ID });
                }
                
                Log.Message("[MemoryPatch] Cleaned up RimTalk API registrations");
            }
            catch (Exception ex)
            {
                Log.Warning($"[MemoryPatch] Cleanup failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 获取确定性 ID
        /// 与 RimTalk 的 PromptEntry.GenerateDeterministicId() 保持一致
        /// </summary>
        private static string GetDeterministicId(string modId, string name)
        {
            // 尝试通过反射调用 RimTalk 的方法（最可靠）
            if (_promptEntryType != null)
            {
                var generateMethod = _promptEntryType.GetMethod("GenerateDeterministicId",
                    BindingFlags.Public | BindingFlags.Static);
                if (generateMethod != null)
                {
                    try
                    {
                        var result = generateMethod.Invoke(null, new object[] { modId, name });
                        if (result is string id)
                        {
                            return id;
                        }
                    }
                    catch { /* 如果反射失败，使用本地实现 */ }
                }
            }
            
            // 本地实现（与 RimTalk 保持一致）
            string sanitizedModId = modId.Replace(".", "_").Replace("-", "_").Replace(" ", "_");
            string sanitizedName = name.Replace(" ", "_").Replace("-", "_");
            return $"mod_{sanitizedModId}_{sanitizedName}";
        }
    }
}