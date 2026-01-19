using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;
using RimTalk.Memory;

namespace RimTalk.Memory.API
{
    /// <summary>
    /// 为 {{knowledge}} 变量提供内容
    ///
    /// 特性：
    /// 1. 支持动态匹配源选择（用户可选择用哪些变量进行匹配）
    /// 2. 关键词匹配（同步执行，无网络依赖）
    /// 3. 从 PromptContext 获取完整上下文
    ///
    /// ⭐ v4.1: 向量增强已移至 Patch_GenerateAndProcessTalkAsync
    /// 原因：Scriban API 是同步调用，向量搜索需要网络请求会阻塞主线程
    ///
    /// ⭐ v5.0: 适配 RimTalk 新版 Scriban 模板系统
    /// - MustacheContext → PromptContext
    /// - MustacheParser → ScribanParser
    /// </summary>
    public static class KnowledgeVariableProvider
    {
        #region 位置追踪（供 Patch 使用）
        
        /// <summary>
        /// 缓存的 knowledge 输出信息，供后台线程的向量增强使用
        /// </summary>
        public class KnowledgeInjectionContext
        {
            public string MatchText { get; set; }           // 用于关键词匹配的文本（配置的匹配源）
            public string DialogueType { get; set; }        // ⭐ 用于向量匹配的文本（固定使用 dialogue.type）
            public string KeywordKnowledge { get; set; }    // 关键词匹配结果
            public Pawn Speaker { get; set; }               // 说话者
            public Pawn Listener { get; set; }              // 听者
            public int Tick { get; set; }                   // 创建时的游戏 tick
        }
        
        // 缓存最近一次 knowledge 注入的上下文
        private static KnowledgeInjectionContext _lastContext;
        private static readonly object _contextLock = new object();
        
        /// <summary>
        /// 获取最近一次 knowledge 注入的上下文（供 Patch 使用）
        /// </summary>
        public static KnowledgeInjectionContext GetLastContext()
        {
            lock (_contextLock)
            {
                return _lastContext;
            }
        }
        
        /// <summary>
        /// 清除缓存的上下文
        /// </summary>
        public static void ClearContext()
        {
            lock (_contextLock)
            {
                _lastContext = null;
            }
        }
        
        #endregion
        
        /// <summary>
        /// 获取匹配的常识内容
        /// 由 RimTalk Scriban Parser 在解析 {{knowledge}} 时调用
        ///
        /// ⭐ v4.1 修复：向量搜索不再在此处执行
        /// 向量搜索会阻塞主线程（因为 Scriban API 是同步的），
        /// 改为通过 Patch_GenerateAndProcessTalkAsync 在异步上下文中执行
        ///
        /// ⭐ v5.0: 参数类型保持 object 以便反射调用，内部会适配 PromptContext/MustacheContext
        /// </summary>
        /// <param name="promptContext">PromptContext 对象（由 RimTalk 传入，可能是 PromptContext 或旧版 MustacheContext）</param>
        /// <returns>格式化的常识文本</returns>
        public static string GetMatchedKnowledge(object promptContext)
        {
            if (promptContext == null)
            {
                return "";
            }
            
            try
            {
                var settings = RimTalkMemoryPatchMod.Settings;
                
                // 1. 根据用户选择的匹配源，构建匹配文本
                string matchText = BuildMatchText(promptContext, settings);
                
                if (string.IsNullOrEmpty(matchText))
                {
                    return "(No context available for matching)";
                }
                
                // 2. 获取 Pawn 信息
                Pawn speaker = GetPropertyValue<Pawn>(promptContext, "CurrentPawn");
                Pawn listener = null;
                
                var allPawns = GetPropertyValue<List<Pawn>>(promptContext, "AllPawns");
                if (allPawns?.Count > 1)
                {
                    listener = allPawns[1];
                }
                
                // 3. 获取常识库
                var memoryManager = Find.World?.GetComponent<MemoryManager>();
                if (memoryManager?.CommonKnowledge == null)
                {
                    return "(No world knowledge available)";
                }
                
                // 4. 关键词匹配（传递pawn信息以支持专属常识过滤）
                // ⭐ 修复：必须传递speaker和listener，否则targetPawnId过滤无法生效
                List<KnowledgeScore> matchedScores;
                string keywordKnowledge = memoryManager.CommonKnowledge.InjectKnowledgeWithDetails(
                    matchText,
                    settings.maxInjectedKnowledge,
                    out matchedScores,
                    speaker,   // currentPawn - 用于匹配专属常识
                    listener   // targetPawn
                );
                
                // 获取 dialogue.type 用于向量搜索（独立于关键词匹配源）
                // ⭐ v5.0: 适配新版 PromptContext，属性名为 DialogueType
                string dialogueType = GetVariableValue(promptContext, "dialogue.type");
                
                // 缓存上下文信息供向量增强 Patch 使用
                lock (_contextLock)
                {
                    _lastContext = new KnowledgeInjectionContext
                    {
                        MatchText = matchText,
                        DialogueType = dialogueType,
                        KeywordKnowledge = keywordKnowledge,
                        Speaker = speaker,
                        Listener = listener,
                        Tick = Find.TickManager?.TicksGame ?? 0
                    };
                }
                
                // 5. 返回关键词匹配结果
                if (string.IsNullOrEmpty(keywordKnowledge))
                {
                    return "(No matching knowledge found)";
                }
                
                return keywordKnowledge;
            }
            catch (Exception ex)
            {
                Log.Warning($"[MemoryPatch] Error getting knowledge: {ex.Message}");
                return "";
            }
        }
        
        /// <summary>
        /// 根据用户选择的匹配源构建匹配文本
        /// v4.1: 支持 Pawn 属性类别自动匹配所有参与对话的 pawn
        /// v5.0: 适配 PromptContext
        /// </summary>
        private static string BuildMatchText(object promptContext, RimTalkMemoryPatchSettings settings)
        {
            var matchTextBuilder = new StringBuilder();
            var sources = settings.knowledgeMatchingSources;
            
            // 如果没有配置匹配源，使用默认
            if (sources == null || sources.Count == 0)
            {
                sources = new List<string> { "dialogue", "context" };
            }
            
            // 获取所有参与对话的 pawn
            var allPawns = GetPropertyValue<List<Pawn>>(promptContext, "AllPawns");
            int pawnCount = allPawns?.Count ?? 0;
            
            foreach (var source in sources)
            {
                // 检查是否是 Pawn 属性（需要自动匹配所有 pawn）
                if (MustacheVariableHelper.IsPawnProperty(source))
                {
                    // ⭐ v5.0: 新格式使用 pawn.xxx，对每个参与对话的 pawn 获取该属性值
                    // 复用 MustacheVariableHelper 的方法获取属性值
                    if (allPawns != null)
                    {
                        foreach (var pawn in allPawns)
                        {
                            if (pawn == null) continue;
                            
                            // 使用 MustacheVariableHelper 获取 pawn 的属性值
                            if (MustacheVariableHelper.TryGetPawnPropertyValue(source, pawn, out string value)
                                && !string.IsNullOrEmpty(value))
                            {
                                if (matchTextBuilder.Length > 0)
                                {
                                    matchTextBuilder.AppendLine();
                                }
                                matchTextBuilder.Append(value);
                            }
                        }
                    }
                }
                else
                {
                    // 普通变量直接获取
                    string value = GetVariableValue(promptContext, source);
                    if (!string.IsNullOrEmpty(value))
                    {
                        if (matchTextBuilder.Length > 0)
                        {
                            matchTextBuilder.AppendLine();
                        }
                        matchTextBuilder.Append(value);
                    }
                }
            }
            
            return matchTextBuilder.ToString();
        }
        
        /// <summary>
        /// 从 PromptContext 获取变量值
        /// 优先使用 PromptContext 的直接属性，其次尝试通过 RimTalk 的 ScribanParser 解析
        /// ⭐ v5.0: 适配新版 Scriban 模板系统
        /// </summary>
        private static string GetVariableValue(object ctx, string variableName)
        {
            try
            {
                // 1. 优先检查 PromptContext 的直接属性
                string directValue = GetDirectContextProperty(ctx, variableName);
                if (!string.IsNullOrEmpty(directValue))
                {
                    return directValue;
                }
                
                // 2. 尝试使用 RimTalk 的 ScribanParser 解析变量
                // 这样可以利用 RimTalk 已有的所有变量解析逻辑
                string parsedValue = TryParseScribanVariable(ctx, variableName);
                if (!string.IsNullOrEmpty(parsedValue))
                {
                    return parsedValue;
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 获取 PromptContext 的直接属性
        /// ⭐ v5.0: 适配新版 PromptContext 属性名
        /// </summary>
        private static string GetDirectContextProperty(object ctx, string variableName)
        {
            // 映射常用变量名到 PromptContext 属性
            switch (variableName)
            {
                case "dialogue.prompt":
                case "prompt":
                    return GetPropertyValue<string>(ctx, "DialoguePrompt");
                case "dialogue.type":
                    return GetPropertyValue<string>(ctx, "DialogueType");
                case "dialogue.status":
                    return GetPropertyValue<string>(ctx, "DialogueStatus");
                case "pawn.context":
                case "context":
                    return GetPropertyValue<string>(ctx, "PawnContext");
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// 尝试使用 RimTalk 的 ScribanParser 解析变量
        /// 这允许我们复用 RimTalk 的所有变量解析逻辑，无需手动枚举
        /// ⭐ v5.0: 优先使用 ScribanParser，兼容旧版 MustacheParser
        /// </summary>
        private static string TryParseScribanVariable(object ctx, string variableName)
        {
            try
            {
                // 查找 RimTalk 程序集
                var rimTalkAssembly = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "RimTalk");
                
                if (rimTalkAssembly == null) return null;
                
                // ⭐ v5.0: 优先使用 ScribanParser
                var parserType = rimTalkAssembly.GetType("RimTalk.Prompt.ScribanParser")
                    ?? rimTalkAssembly.GetType("RimTalk.Prompt.MustacheParser");
                    
                if (parserType == null) return null;
                
                // 尝试调用 Render/Parse 方法，解析单个变量
                // Scriban 使用 {{variableName}} 语法
                string template = "{{" + variableName + "}}";
                
                // ScribanParser 使用 Render 方法，MustacheParser 使用 Parse 方法
                var renderMethod = parserType.GetMethod("Render", BindingFlags.Public | BindingFlags.Static)
                    ?? parserType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static);
                
                if (renderMethod != null)
                {
                    // Render(string template, PromptContext context, bool logErrors = true)
                    // 或 Parse(string template, MustacheContext context)
                    object[] args;
                    var parameters = renderMethod.GetParameters();
                    if (parameters.Length >= 3)
                    {
                        args = new object[] { template, ctx, false }; // logErrors = false
                    }
                    else
                    {
                        args = new object[] { template, ctx };
                    }
                    
                    var result = renderMethod.Invoke(null, args);
                    
                    // 如果解析成功且结果不是原模板，返回结果
                    string parsed = result as string;
                    if (!string.IsNullOrEmpty(parsed) && parsed != template)
                    {
                        return parsed;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning($"[MemoryPatch] TryParseScribanVariable failed: {ex.Message}");
                }
                return null;
            }
        }
        // ⭐ v4.1: GetVectorEnhancedKnowledge 和 CombineKnowledge 方法已移除
        // 向量搜索现在通过 Patch_GenerateAndProcessTalkAsync 在异步上下文中执行
        // 这避免了在 Mustache API 同步调用中阻塞主线程
        
        
        #region Helper Methods
        
        private static T GetPropertyValue<T>(object obj, string propertyName) where T : class
        {
            if (obj == null) return null;
            
            var prop = obj.GetType().GetProperty(propertyName);
            if (prop != null)
            {
                return prop.GetValue(obj) as T;
            }
            return null;
        }
        
        private static Pawn GetCurrentPawn(object ctx)
        {
            return GetPropertyValue<Pawn>(ctx, "CurrentPawn");
        }
        
        private static Pawn GetPawn2(object ctx)
        {
            var allPawns = GetPropertyValue<List<Pawn>>(ctx, "AllPawns");
            return allPawns?.Count > 1 ? allPawns[1] : null;
        }
        
        private static Map GetMap(object ctx)
        {
            return GetPropertyValue<Map>(ctx, "Map");
        }
        
        #endregion
    }
}