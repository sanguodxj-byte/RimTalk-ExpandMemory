using Verse;
using UnityEngine;
using HarmonyLib;
using System;

namespace RimTalk.MemoryPatch
{
    public class RimTalkMemoryPatchMod : Mod
    {
        public static RimTalkMemoryPatchSettings _settings;

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

            // ⭐ v3.3.2.5: 强制预注册关键类型，确保旧存档兼容性
            Memory.BackCompatibilityFix.ForceInitialize();

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
        }
    }
}
