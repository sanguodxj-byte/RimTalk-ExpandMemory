using RimTalk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimTalkHistoryPlus // 等待接入主UI
{
    public static class RimTalkHistoryPlusUI
    {
        public static void DoRimTalkHistoryPlusUI(Rect inRect, RimTalkHistoryPlusSettings settings) 
        {

            var listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            // 标题
            listingStandard.Label("<b>存储设置 (Storage)</b>");

            // 最大历史条目数
            listingStandard.Label($"最大保存历史条数: {settings.MaxHistory} (不计入被固定后的记忆/历史，建议调高点)");
            settings.MaxHistory = (int)listingStandard.Slider(settings.MaxHistory, min: 10, max:500);
            listingStandard.Gap();

            // 单条历史最大字数
            listingStandard.Label($"单条历史最大字数: {settings.MaxTextBlockLength}");
            settings.MaxTextBlockLength = (int)listingStandard.Slider(settings.MaxTextBlockLength, min: settings.MaxTextBlockInjectedLength, max:5000);

            listingStandard.GapLine();

            // 标题
            listingStandard.Label("<b>注入设置 (Injection)</b>");

            // 注入条目数
            listingStandard.Label($"注入到Prompt的最大历史条数: {settings.MaxHistoryInjected} （本配置项无效，请使用主设置页面的注入上限配置项）");
            settings.MaxHistoryInjected = (int)listingStandard.Slider(settings.MaxHistoryInjected, min:1, max:20);
            listingStandard.Gap();

            // 注入时单条历史最大文本长度
            listingStandard.Label($"注入时单条历史最大文本长度: {settings.MaxTextBlockInjectedLength} （可以限制每一条历史的注入文本上限，超出部分会用“...”截断）");
            settings.MaxTextBlockInjectedLength = (int)listingStandard.Slider(settings.MaxTextBlockInjectedLength, min: 100, max: settings.MaxTextBlockLength);
            listingStandard.Gap();

            // 注入总字数
            listingStandard.Label($"注入到Prompt的历史的最大总字数: {settings.MaxInjectedLength} （取决于你的token预算）");
            settings.MaxInjectedLength = (int)listingStandard.Slider(settings.MaxInjectedLength, min: settings.MaxTextBlockInjectedLength, max:10000);

            // 可以在这里添加一个重置按钮
            listingStandard.Gap();
            if (listingStandard.ButtonText("重置为默认值"))
            {
                settings.MaxHistory = 200;
                settings.MaxTextBlockLength = 1000;
                settings.MaxHistoryInjected = 4;
                settings.MaxTextBlockInjectedLength = 1000;
                settings.MaxInjectedLength = 2000;
            }

            listingStandard.End();
        }
    }
}
