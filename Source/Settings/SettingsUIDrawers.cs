using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.Memory;
using System.Linq;

namespace RimTalk.MemoryPatch
{
    /// <summary>
    /// 设置UI绘制辅助类 - 拆分UI代码以减少主文件大小
    /// ★ v3.3.20: 模块化设置界面
    /// </summary>
    public static class SettingsUIDrawers
    {
        // ==================== AI配置绘制 ====================
        
        public static void DrawAIProviderSelection(Listing_Standard listing, RimTalkMemoryPatchSettings settings)
        {
            listing.Label("AI 提供商:");
            GUI.color = Color.gray;
            listing.Label($"  当前: {settings.independentProvider}");
            GUI.color = Color.white;
            
            // 提供商选择按钮
            Rect providerHeaderRect = listing.GetRect(25f);
            Widgets.DrawBoxSolid(providerHeaderRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            Widgets.Label(providerHeaderRect.ContractedBy(5f), "选择提供商");
            
            Rect providerButtonRect1 = listing.GetRect(30f);
            float buttonWidth = (providerButtonRect1.width - 20f) / 3f;
            
            // 第一行：OpenAI, DeepSeek, Player2
            DrawProviderButton(new Rect(providerButtonRect1.x, providerButtonRect1.y, buttonWidth, 30f), 
                "OpenAI", settings, "OpenAI", "gpt-3.5-turbo", "https://api.openai.com/v1/chat/completions",
                new Color(0.5f, 1f, 0.5f));
            
            DrawProviderButton(new Rect(providerButtonRect1.x + buttonWidth + 10f, providerButtonRect1.y, buttonWidth, 30f),
                "DeepSeek", settings, "DeepSeek", "deepseek-chat", "https://api.deepseek.com/v1/chat/completions",
                new Color(0.5f, 0.7f, 1f));
            
            DrawProviderButton(new Rect(providerButtonRect1.x + 2 * (buttonWidth + 10f), providerButtonRect1.y, buttonWidth, 30f),
                "Player2", settings, "Player2", "gpt-4o", "https://api.player2.game/v1/chat/completions",
                new Color(1f, 0.8f, 0.5f));
            
            // 第二行：Google, Custom
            Rect providerButtonRect2 = listing.GetRect(30f);
            
            DrawProviderButton(new Rect(providerButtonRect2.x, providerButtonRect2.y, buttonWidth, 30f),
                "Google", settings, "Google", "gemini-2.0-flash-exp", "https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent",
                new Color(1f, 0.5f, 0.5f));
            
            DrawProviderButton(new Rect(providerButtonRect2.x + buttonWidth + 10f, providerButtonRect2.y, buttonWidth, 30f),
                "Custom", settings, "Custom", "custom-model", "https://your-api-url.com/v1/chat/completions",
                new Color(0.7f, 0.7f, 0.7f));
            
            GUI.color = Color.white;
            listing.Gap();
            
            // 提供商说明
            DrawProviderDescription(listing, settings.independentProvider);
        }
        
        private static void DrawProviderButton(Rect rect, string label, RimTalkMemoryPatchSettings settings, 
            string provider, string model, string url, Color highlightColor)
        {
            bool isSelected = settings.independentProvider == provider;
            GUI.color = isSelected ? highlightColor : Color.white;
            
            if (Widgets.ButtonText(rect, label))
            {
                settings.independentProvider = provider;
                settings.independentModel = model;
                settings.independentApiUrl = url;
            }
            
            GUI.color = Color.white;
        }
        
        private static void DrawProviderDescription(Listing_Standard listing, string provider)
        {
            GUI.color = new Color(0.7f, 0.9f, 1f);
            
            switch (provider)
            {
                case "OpenAI":
                    listing.Label("? OpenAI GPT 系列模型，稳定可靠");
                    listing.Label("   推荐模型: gpt-3.5-turbo, gpt-4");
                    break;
                case "DeepSeek":
                    listing.Label("? DeepSeek 中文优化模型，性价比高");
                    listing.Label("   推荐模型: deepseek-chat, deepseek-coder");
                    break;
                case "Player2":
                    listing.Label("? Player2 游戏优化 AI，支持本地客户端");
                    listing.Label("   推荐模型: gpt-4o, gpt-4-turbo");
                    break;
                case "Google":
                    listing.Label("? Google Gemini 系列，多模态能力强");
                    listing.Label("   推荐模型: gemini-2.0-flash-exp");
                    break;
                case "Custom":
                    listing.Label("? 自定义 API 端点，支持第三方代理");
                    listing.Label("   请手动配置 API URL 和 Model");
                    break;
            }
            
            GUI.color = Color.white;
        }
        
        // ==================== 常识链设置绘制 ====================
        
        public static void DrawKnowledgeChainingSettings(Listing_Standard listing, RimTalkMemoryPatchSettings settings)
        {
            // 常识链设置（实验性功能）
            listing.CheckboxLabeled("启用常识链（实验性）", ref settings.enableKnowledgeChaining);
            if (settings.enableKnowledgeChaining)
            {
                GUI.color = new Color(1f, 0.8f, 0.5f);
                listing.Label("  允许常识触发常识，进行多轮匹配");
                GUI.color = Color.white;
                listing.Gap();
                
                listing.Label($"最大轮数: {settings.maxChainingRounds}");
                settings.maxChainingRounds = (int)listing.Slider(settings.maxChainingRounds, 1, 5);
            }
            
            listing.Gap();
        }
        
        // ==================== 提示词规范化设置绘制 ====================
        
        public static void DrawPromptNormalizationSettings(Listing_Standard listing, RimTalkMemoryPatchSettings settings)
        {
            // 背景框
            Rect sectionRect = listing.GetRect(300f);
            Widgets.DrawBoxSolid(sectionRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            
            Listing_Standard inner = new Listing_Standard();
            inner.Begin(sectionRect.ContractedBy(10f));
            
            // 标题
            Text.Font = GameFont.Small;
            GUI.color = new Color(1f, 0.9f, 0.7f);
            inner.Label("替换规则列表");
            GUI.color = Color.white;
            
            inner.Gap(5f);
            
            // 规则列表
            if (settings.normalizationRules == null)
            {
                settings.normalizationRules = new System.Collections.Generic.List<RimTalkMemoryPatchSettings.ReplacementRule>();
            }
            
            // 绘制每条规则
            for (int i = 0; i < settings.normalizationRules.Count; i++)
            {
                var rule = settings.normalizationRules[i];
                
                Rect ruleRect = inner.GetRect(30f);
                
                // 启用复选框
                Rect checkboxRect = new Rect(ruleRect.x, ruleRect.y, 24f, 24f);
                Widgets.Checkbox(checkboxRect.position, ref rule.isEnabled);
                
                // 模式输入框
                Rect patternRect = new Rect(ruleRect.x + 30f, ruleRect.y, 200f, 25f);
                rule.pattern = Widgets.TextField(patternRect, rule.pattern ?? "");
                
                // 箭头
                Rect arrowRect = new Rect(ruleRect.x + 235f, ruleRect.y, 30f, 25f);
                Widgets.Label(arrowRect, " → ");
                
                // 替换输入框
                Rect replacementRect = new Rect(ruleRect.x + 270f, ruleRect.y, 150f, 25f);
                rule.replacement = Widgets.TextField(replacementRect, rule.replacement ?? "");
                
                // 删除按钮
                Rect deleteRect = new Rect(ruleRect.x + 430f, ruleRect.y, 30f, 25f);
                GUI.color = new Color(1f, 0.3f, 0.3f);
                if (Widgets.ButtonText(deleteRect, "×"))
                {
                    settings.normalizationRules.RemoveAt(i);
                    i--;
                }
                GUI.color = Color.white;
                
                inner.Gap(3f);
            }
            
            // 添加新规则按钮
            Rect addButtonRect = inner.GetRect(30f);
            if (Widgets.ButtonText(addButtonRect, "+ 添加新规则"))
            {
                settings.normalizationRules.Add(new RimTalkMemoryPatchSettings.ReplacementRule("", "", true));
            }
            
            inner.Gap(5f);
            
            // 统计信息
            int enabledCount = settings.normalizationRules.Count(r => r.isEnabled);
            GUI.color = Color.gray;
            inner.Label($"已启用: {enabledCount} / {settings.normalizationRules.Count} 条规则");
            GUI.color = Color.white;
            
            // 示例提示
            inner.Gap(3f);
            GUI.color = new Color(0.7f, 0.9f, 1f);
            inner.Label("? 示例：模式 \\(Player\\) → 替换 Master");
            inner.Label("   支持正则表达式（忽略大小写）");
            GUI.color = Color.white;
            
            inner.End();
        }
        
        // ==================== 向量增强设置绘制 ====================
        
        public static void DrawSiliconFlowSettings(Listing_Standard listing, RimTalkMemoryPatchSettings settings)
        {
            listing.CheckboxLabeled("启用向量增强", ref settings.enableVectorEnhancement);
            if (settings.enableVectorEnhancement)
            {
                GUI.color = new Color(0.8f, 1f, 0.8f);
                listing.Label("  使用语义向量检索来增强常识匹配。");
                GUI.color = Color.white;
                listing.Gap();
                
                listing.Label($"相似度阈值: {settings.vectorSimilarityThreshold:F2}");
                settings.vectorSimilarityThreshold = listing.Slider(settings.vectorSimilarityThreshold, 0.5f, 1.0f);
                
                listing.Label($"最大补充结果: {settings.maxVectorResults}");
                settings.maxVectorResults = (int)listing.Slider(settings.maxVectorResults, 1, 15);
                
                listing.Gap();
                
                GUI.color = new Color(1f, 0.9f, 0.7f);
                listing.Label("云端 Embedding 配置");
                GUI.color = Color.white;
                
                listing.Label("API Key:");
                settings.embeddingApiKey = listing.TextEntry(settings.embeddingApiKey);
                
                listing.Label("API URL:");
                settings.embeddingApiUrl = listing.TextEntry(settings.embeddingApiUrl);
                
                listing.Label("Model:");
                settings.embeddingModel = listing.TextEntry(settings.embeddingModel);
                
                GUI.color = Color.gray;
                listing.Label("提示: 留空 API Key 将使用上方 AI 配置中的 API Key");
                GUI.color = Color.white;
            }
            
            listing.Gap();
        }
    }
}
