using Verse;
using UnityEngine;
using HarmonyLib;
using System;
using System.Text;

namespace RimTalk.MemoryPatch
{
    public class RimTalkMemoryPatchMod : Mod
    {
        public static RimTalkMemoryPatchSettings _settings;
        private int _apiSettingsHash = 0;

        /// <summary>
        /// 获取当前 Mod 的设置实例
        /// 下游调用时无需判空——若为空则说明出现重大错误，允许直接抛异常
        /// 额外的，通过属性封装，这里直接显式声明上述意图
        /// </summary>
        public static RimTalkMemoryPatchSettings Settings
        {
            get
            {
                if (_settings is null)
                    throw new Exception("[RimTalk.Memory] RimTalkMemoryPatchSettings is null");

                return _settings;
            }
        }

        public RimTalkMemoryPatchMod(ModContentPack content) : base(content)
        {
            _settings = GetSettings<RimTalkMemoryPatchSettings>();
            _apiSettingsHash = GetApiSettingsHash(_settings);

            // ⭐ 初始化提示词规范化器
            Memory.PromptNormalizer.UpdateRules(Settings.normalizationRules);

            var harmony = new Harmony("cj.rimtalk.expandmemory");
            harmony.PatchAll();
            Log.Message("[RimTalk-Expand Memory] Loaded successfully");

            if (Prefs.DevMode)
            {
                Log.Message($"[PromptNormalizer] Initialized with {Memory.PromptNormalizer.GetActiveRuleCount()} active rules");
            }
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoSettingsWindowContents(inRect);
            base.DoSettingsWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "RimTalk-Expand Memory";
        }

        public override void WriteSettings()
        {
            base.WriteSettings();

            // 重新加载提示词规范化规则
            Memory.PromptNormalizer.UpdateRules(Settings.normalizationRules);

            var settings = Settings;
            int newHash = GetApiSettingsHash(settings);

            if (newHash != _apiSettingsHash)
            {
                _apiSettingsHash = newHash;
                Memory.AI.AIService.ResetClientPool();
                Log.Message("[RimTalk-Expand Memory] AI config changed, client pool reset.");
            }
        }

        private int GetApiSettingsHash(RimTalkMemoryPatchSettings settings)
        {
            var sb = new StringBuilder();

            sb.AppendLine(settings.UseRimTalkAIConfig.ToString());

            if (settings.ApiConfigs != null)
            {
                foreach (var config in settings.ApiConfigs)
                {
                    sb.AppendLine(config.Provider.ToString());
                    sb.AppendLine(config.ApiKey);
                    sb.AppendLine(config.CustomModelName);
                    sb.AppendLine(config.CustomUrl);
                    sb.AppendLine(config.IsEnabled.ToString());
                }
            }

            return sb.ToString().GetHashCode();
        }
    }
}
