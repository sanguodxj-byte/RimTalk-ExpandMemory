using UnityEngine;
using Verse;

namespace RimTalkHistoryPlus // 等待接入主UI后修改
{
    public class RimTalkHistoryPlusMod : Mod
    {
        // 静态实例，方便在其他代码中访问 Settings
        public static RimTalkHistoryPlusMod Instance;
        public static RimTalkHistoryPlusSettings Settings;

        public RimTalkHistoryPlusMod(ModContentPack content) : base(content)
        {
            // 初始化设置
            Instance = this;
            Settings = GetSettings<RimTalkHistoryPlusSettings>();
        }

        // 设置菜单显示的名称
        public override string SettingsCategory()
        {
            return "等待配置项外接";
        }

        // 绘制设置界面
        public override void DoSettingsWindowContents(Rect inRect)
        {
            RimTalkHistoryPlusUI.DoRimTalkHistoryPlusUI(inRect, Settings);
        }
    }
}