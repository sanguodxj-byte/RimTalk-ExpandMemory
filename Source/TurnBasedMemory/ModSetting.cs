using Verse;
using UnityEngine;

namespace RimTalkHistoryPlus // 等待接入主UI后修改
{
    public class RimTalkHistoryPlusSettings : ModSettings
    {
        // 默认值定义
        public int MaxHistory = 200; // 最大保存历史条目数
        public int MaxTextBlockLength = 1000; // 保存时单条历史最大文本长度
        public int MaxHistoryInjected = 4; // 最大注入历史条目数
        public int MaxTextBlockInjectedLength = 1000; // 注入时单条历史最大文本长度
        public int MaxInjectedLength = 2000; // 注入时最大总文本长度
        public bool UpdateAvailable_20260126 = true; // 是否有更新可用

        // 保存/加载逻辑
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref MaxHistory, "MaxHistory", 200);
            Scribe_Values.Look(ref MaxTextBlockLength, "MaxTextBlockLength", 1000);
            Scribe_Values.Look(ref MaxHistoryInjected, "MaxHistoryInjected", 4);
            Scribe_Values.Look(ref MaxTextBlockInjectedLength, "MaxTextBlockInjectedLength", 1000);
            Scribe_Values.Look(ref MaxInjectedLength, "MaxInjectedLength", 2000);
            Scribe_Values.Look(ref UpdateAvailable_20260126, "UpdateAvailable_20260126", true);
        }
    }
}