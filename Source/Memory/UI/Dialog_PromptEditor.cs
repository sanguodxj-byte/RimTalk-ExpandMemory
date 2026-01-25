using UnityEngine;
using Verse;
using RimWorld;
using RimTalk.MemoryPatch;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// AI 总结提示词编辑对话框
    /// </summary>
    public class Dialog_PromptEditor : Window
    {
        private RimTalkMemoryPatchSettings settings;
        
        // 临时编辑变量
        private string editDailySummary;
        private string editDeepArchive;
        private int editMaxTokens;
        
        // 默认提示词（从 IndependentAISummarizer 复制）
        private const string DEFAULT_DAILY_SUMMARY = 
            "殖民者{0}的记忆总结\n\n" +
            "记忆列表\n" +
            "{1}\n\n" +
            "要求提炼地点人物事件\n" +
            "相似事件合并标注频率\n" +
            "极简表达不超过80字\n" +
            "只输出总结文字不要其他格式";
        
        private const string DEFAULT_DEEP_ARCHIVE = 
            "殖民者{0}的记忆归档\n\n" +
            "记忆列表\n" +
            "{1}\n\n" +
            "要求提炼核心特征和里程碑事件\n" +
            "合并相似经历突出长期趋势\n" +
            "极简表达不超过60字\n" +
            "只输出总结文字不要其他格式";
        
        private Vector2 scrollPosition = Vector2.zero;
        
        public override Vector2 InitialSize => new Vector2(800f, 650f);
        
        public Dialog_PromptEditor()
        {
            this.settings = RimTalkMemoryPatchMod.Settings;
            
            doCloseX = true;
            doCloseButton = false;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            
            // 初始化编辑变量
            editDailySummary = string.IsNullOrEmpty(settings.dailySummaryPrompt) 
                ? DEFAULT_DAILY_SUMMARY 
                : settings.dailySummaryPrompt;
                
            editDeepArchive = string.IsNullOrEmpty(settings.deepArchivePrompt) 
                ? DEFAULT_DEEP_ARCHIVE 
                : settings.deepArchivePrompt;
                
            editMaxTokens = settings.summaryMaxTokens;
        }
        
        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(0f, 0f, inRect.width, 35f);
            Widgets.Label(titleRect, "RimTalk_PromptEditor_Title".Translate());
            
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            Rect descRect = new Rect(0f, 35f, inRect.width, 20f);
            Widgets.Label(descRect, "RimTalk_PromptEditor_Desc".Translate());
            GUI.color = Color.white;
            
            // 内容区域
            float contentY = 60f;
            float contentHeight = inRect.height - contentY - 50f; // 留出底部按钮空间
            Rect contentRect = new Rect(0f, contentY, inRect.width, contentHeight);
            
            DrawContent(contentRect);
            
            // 底部按钮
            float buttonY = inRect.height - 40f;
            float buttonWidth = 120f;
            float spacing = 10f;
            
            // 恢复默认按钮（左侧）
            Rect resetRect = new Rect(0f, buttonY, buttonWidth, 35f);
            if (Widgets.ButtonText(resetRect, "RimTalk_PromptEditor_ResetDefault".Translate()))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "RimTalk_PromptEditor_ResetConfirm".Translate(),
                    delegate
                    {
                        editDailySummary = DEFAULT_DAILY_SUMMARY;
                        editDeepArchive = DEFAULT_DEEP_ARCHIVE;
                        editMaxTokens = 200;
                    }
                ));
            }
            
            // 取消和保存按钮（右侧）
            float rightX = inRect.width - buttonWidth;
            Rect saveRect = new Rect(rightX, buttonY, buttonWidth, 35f);
            if (Widgets.ButtonText(saveRect, "RimTalk_Save".Translate()))
            {
                SaveAndClose();
            }
            
            rightX -= buttonWidth + spacing;
            Rect cancelRect = new Rect(rightX, buttonY, buttonWidth, 35f);
            if (Widgets.ButtonText(cancelRect, "RimTalk_Cancel".Translate()))
            {
                Close();
            }
        }
        
        private void DrawContent(Rect rect)
        {
            Listing_Standard listing = new Listing_Standard();
            Rect viewRect = new Rect(0f, 0f, rect.width - 20f, 900f);
            
            Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
            listing.Begin(viewRect);
            
            // 每日总结提示词
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.9f, 1f);
            listing.Label("RimTalk_PromptEditor_DailySummary".Translate());
            GUI.color = Color.white;
            
            GUI.color = Color.gray;
            listing.Label("RimTalk_PromptEditor_DailySummaryDesc".Translate());
            listing.Label("RimTalk_PromptEditor_Placeholders".Translate());
            GUI.color = Color.white;
            listing.Gap(4f);
            
            Rect dailyRect = listing.GetRect(180f);
            editDailySummary = Widgets.TextArea(dailyRect, editDailySummary);
            
            listing.Gap(15f);
            listing.GapLine();
            listing.Gap(10f);
            
            // 深度归档提示词
            GUI.color = new Color(0.8f, 0.9f, 1f);
            listing.Label("RimTalk_PromptEditor_DeepArchive".Translate());
            GUI.color = Color.white;
            
            GUI.color = Color.gray;
            listing.Label("RimTalk_PromptEditor_DeepArchiveDesc".Translate());
            listing.Label("RimTalk_PromptEditor_Placeholders".Translate());
            GUI.color = Color.white;
            listing.Gap(4f);
            
            Rect archiveRect = listing.GetRect(180f);
            editDeepArchive = Widgets.TextArea(archiveRect, editDeepArchive);
            
            listing.Gap(15f);
            listing.GapLine();
            listing.Gap(10f);
            
            // Max Tokens 滑块
            GUI.color = new Color(0.8f, 0.9f, 1f);
            listing.Label("RimTalk_PromptEditor_MaxTokens".Translate());
            GUI.color = Color.white;
            
            GUI.color = Color.gray;
            listing.Label("RimTalk_PromptEditor_MaxTokensDesc".Translate());
            GUI.color = Color.white;
            listing.Gap(4f);
            
            listing.Label("RimTalk_PromptEditor_MaxTokensLabel".Translate(editMaxTokens));
            editMaxTokens = (int)listing.Slider(editMaxTokens, 100, 8000);
            
            // 提示信息
            listing.Gap(10f);
            GUI.color = new Color(1f, 0.9f, 0.6f);
            listing.Label("RimTalk_PromptEditor_Tips".Translate());
            GUI.color = Color.gray;
            listing.Label("RimTalk_PromptEditor_Tip1".Translate());
            listing.Label("RimTalk_PromptEditor_Tip2".Translate());
            listing.Label("RimTalk_PromptEditor_Tip3".Translate());
            GUI.color = Color.white;
            
            listing.End();
            Widgets.EndScrollView();
        }
        
        private void SaveAndClose()
        {
            // 保存到设置
            // 如果与默认值相同，保存为空字符串（表示使用默认）
            settings.dailySummaryPrompt = (editDailySummary == DEFAULT_DAILY_SUMMARY) 
                ? "" 
                : editDailySummary;
                
            settings.deepArchivePrompt = (editDeepArchive == DEFAULT_DEEP_ARCHIVE) 
                ? "" 
                : editDeepArchive;
                
            settings.summaryMaxTokens = editMaxTokens;
            
            // 保存设置
            settings.Write();
            
            Messages.Message("RimTalk_PromptEditor_Saved".Translate(), MessageTypeDefOf.PositiveEvent, false);
            
            Close();
        }
    }
}
