using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;

namespace RimTalk.Memory.UI
{
    /// <summary>
    /// 标签测试工具弹窗
    /// </summary>
    public class Dialog_TagTest : Window
    {
        private string testTag = "";
        private string testContext = "";
        private Pawn testPawn = null;
        private bool testResult = false;
        private string testMatchText = "";
        private bool testExecuted = false;
        private Vector2 scrollPosition = Vector2.zero;

        public override Vector2 InitialSize => new Vector2(650f, 600f);

        public Dialog_TagTest()
        {
            this.doCloseX = true;
            this.doCloseButton = false;
            this.closeOnClickedOutside = false;
            this.absorbInputAroundWindow = true;
            this.forcePause = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = 0f;
            
            // 标题
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, inRect.width, 35f), 
                CommonKnowledgeTranslationKeys.TagTestTitle.Translate());
            Text.Font = GameFont.Small;
            y += 40f;
            
            // ========== 输入区域 =========
            
            // 测试标签输入
            Widgets.Label(new Rect(0f, y, inRect.width, 22f), 
                CommonKnowledgeTranslationKeys.TagTestInputTag.Translate());
            y += 22f;
            testTag = Widgets.TextField(new Rect(0f, y, inRect.width, 28f), testTag);
            y += 32f;
            
            // 对话内容输入
            Widgets.Label(new Rect(0f, y, inRect.width, 22f), 
                CommonKnowledgeTranslationKeys.TagTestInputContext.Translate());
            y += 22f;
            testContext = Widgets.TextArea(new Rect(0f, y, inRect.width, 50f), testContext);
            y += 54f;
            
            // Pawn 选择
            Widgets.Label(new Rect(0f, y, inRect.width, 22f), 
                CommonKnowledgeTranslationKeys.TagTestSelectPawn.Translate());
            y += 22f;
            
            string pawnLabel = testPawn == null 
                ? CommonKnowledgeTranslationKeys.TagTestNoPawn.Translate().ToString()
                : testPawn.LabelShort;
            
            if (Widgets.ButtonText(new Rect(0f, y, inRect.width, 28f), pawnLabel))
            {
                ShowPawnSelectionMenu();
            }
            y += 32f;
            
            // 测试按钮和清除按钮
            float buttonWidth = (inRect.width - 10f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, y, buttonWidth, 32f), "?? 测试"))
            {
                ExecuteTagTest();
            }
            
            if (Widgets.ButtonText(new Rect(buttonWidth + 10f, y, buttonWidth, 32f), 
                CommonKnowledgeTranslationKeys.TagTestClear.Translate()))
            {
                testTag = "";
                testContext = "";
                testPawn = null;
                testExecuted = false;
            }
            y += 40f;
            
            // ========== 分隔线 =========
            Widgets.DrawLineHorizontal(0f, y, inRect.width);
            y += 15f;
            
            // ========== 结果区域（始终显示，未测试时显示提示）==========
            float resultAreaHeight = inRect.height - y;
            Rect resultRect = new Rect(0f, y, inRect.width, resultAreaHeight);
            
            if (testExecuted)
            {
                DrawResultArea(resultRect);
            }
            else
            {
                DrawEmptyResultArea(resultRect);
            }
        }
        
        private void DrawEmptyResultArea(Rect rect)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = new Color(0.6f, 0.6f, 0.6f);
            Widgets.Label(rect, "点击 ?? 测试 按钮开始测试");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawResultArea(Rect rect)
        {
            float y = rect.y; // 使用绝对坐标
            
            // 匹配结果标题
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.x, y, rect.width, 25f), 
                CommonKnowledgeTranslationKeys.TagTestResult.Translate());
            y += 25f;
            
            // 匹配结果
            Text.Font = GameFont.Medium;
            if (testResult)
            {
                GUI.color = new Color(0.3f, 0.9f, 0.3f);
                Widgets.Label(new Rect(rect.x, y, rect.width, 30f), 
                    CommonKnowledgeTranslationKeys.TagTestMatched.Translate());
            }
            else
            {
                GUI.color = new Color(0.9f, 0.3f, 0.3f);
                Widgets.Label(new Rect(rect.x, y, rect.width, 30f), 
                    CommonKnowledgeTranslationKeys.TagTestNotMatched.Translate());
            }
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            y += 35f;
            
            // 实际匹配文本标题
            Widgets.Label(new Rect(rect.x, y, rect.width, 25f), 
                CommonKnowledgeTranslationKeys.TagTestMatchText.Translate());
            y += 25f;
            
            // 实际匹配文本内容（可滚动）
            float scrollAreaHeight = rect.yMax - y;
            Rect scrollOuterRect = new Rect(rect.x, y, rect.width, scrollAreaHeight);
            
            // 计算内容高度
            Text.Font = GameFont.Tiny;
            float contentHeight = Text.CalcHeight(testMatchText, rect.width - 20f);
            Text.Font = GameFont.Small;
            
            Rect scrollViewRect = new Rect(0f, 0f, rect.width - 16f, Mathf.Max(contentHeight + 20f, scrollAreaHeight));
            
            Widgets.BeginScrollView(scrollOuterRect, ref scrollPosition, scrollViewRect);
            
            // 绘制匹配文本背景
            Rect textBoxRect = new Rect(5f, 5f, scrollViewRect.width - 10f, contentHeight + 10f);
            Widgets.DrawBoxSolid(textBoxRect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            
            // 绘制匹配文本
            Rect textRect = textBoxRect.ContractedBy(5f);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.9f, 0.9f, 0.9f);
            Widgets.Label(textRect, testMatchText);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            
            Widgets.EndScrollView();
        }

        private void ShowPawnSelectionMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            
            // 无 Pawn 选项
            options.Add(new FloatMenuOption(
                CommonKnowledgeTranslationKeys.TagTestNoPawn.Translate(),
                delegate { testPawn = null; }
            ));
            
            // 当前地图的所有 Pawn
            if (Find.CurrentMap != null)
            {
                var allPawns = Find.CurrentMap.mapPawns.AllPawns
                    .Where(p => p.RaceProps.Humanlike)
                    .OrderBy(p => p.Faction != Faction.OfPlayer)
                    .ThenBy(p => p.LabelShort);
                
                foreach (var pawn in allPawns)
                {
                    string label = $"{pawn.LabelShort}";
                    if (pawn.Faction != null)
                    {
                        label += $" ({pawn.Faction.Name})";
                    }
                    
                    Pawn localPawn = pawn; // 避免闭包问题
                    options.Add(new FloatMenuOption(label, delegate { testPawn = localPawn; }));
                }
            }
            
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ExecuteTagTest()
        {
            if (string.IsNullOrWhiteSpace(testTag))
            {
                Messages.Message("请输入标签", MessageTypeDefOf.RejectInput, false);
                return;
            }
            
            // 创建临时常识条目
            var tempEntry = new CommonKnowledgeEntry(testTag, "测试内容")
            {
                matchMode = KeywordMatchMode.Any
            };
            
            // 构建匹配文本
            System.Text.StringBuilder matchTextBuilder = new System.Text.StringBuilder();
            matchTextBuilder.Append(testContext);
            
            if (testPawn != null)
            {
                matchTextBuilder.Append(" ");
                matchTextBuilder.Append(BuildPawnInfoText(testPawn));
            }
            
            testMatchText = matchTextBuilder.ToString();
            
            // 测试匹配
            testResult = TestTagMatch(testMatchText, tempEntry);
            testExecuted = true;
            
            // 显示详细的匹配文本信息
            Log.Message($"[标签测试] 标签: {testTag}");
            Log.Message($"[标签测试] 对话: {testContext}");
            Log.Message($"[标签测试] Pawn: {(testPawn != null ? testPawn.LabelShort : "无")}");
            Log.Message($"[标签测试] 实际匹配文本: {testMatchText}");
            Log.Message($"[标签测试] 结果: {(testResult ? "匹配成功" : "不匹配")}");
        }

        /// <summary>
        /// 构建完整的 Pawn 信息文本（与 CommonKnowledgeLibrary 保持一致）
        /// </summary>
        private string BuildPawnInfoText(Pawn pawn)
        {
            if (pawn == null)
                return string.Empty;

            var sb = new System.Text.StringBuilder();

            try
            {
                // 1. 名字
                if (!string.IsNullOrEmpty(pawn.Name?.ToStringShort))
                {
                    sb.Append(pawn.Name.ToStringShort);
                    sb.Append(" ");
                }

                // 2. 年龄段
                if (pawn.RaceProps != null && pawn.RaceProps.Humanlike)
                {
                    float ageYears = pawn.ageTracker.AgeBiologicalYearsFloat;
                    
                    if (ageYears < 3f)
                    {
                        sb.Append("婴儿 宝宝 ");
                    }
                    else if (ageYears < 13f)
                    {
                        sb.Append("儿童 小孩 ");
                    }
                    else if (ageYears < 18f)
                    {
                        sb.Append("青少年 ");
                    }
                    else
                    {
                        sb.Append("成人 ");
                    }
                }

                // 3. 性别
                if (pawn.gender != null)
                {
                    sb.Append(pawn.gender.GetLabel());
                    sb.Append(" ");
                }

                // 4. 种族
                if (pawn.def != null)
                {
                    sb.Append(pawn.def.label);
                    sb.Append(" ");
                    
                    // 亚种信息（Biotech DLC）
                    try
                    {
                        if (pawn.genes != null && pawn.genes.Xenotype != null)
                        {
                            string xenotypeName = pawn.genes.Xenotype.label ?? pawn.genes.Xenotype.defName;
                            if (!string.IsNullOrEmpty(xenotypeName))
                            {
                                sb.Append(xenotypeName);
                                sb.Append(" ");
                            }
                        }
                    }
                    catch { /* 兼容性：没有Biotech DLC时跳过 */ }
                }

                // 4.5. 身份（殖民者/囚犯/奴隶/访客）
                if (pawn.IsColonist)
                {
                    sb.Append("殖民者 ");
                }
                else if (pawn.IsPrisoner)
                {
                    sb.Append("囚犯 ");
                }
                else if (pawn.IsSlaveOfColony)
                {
                    sb.Append("奴隶 ");
                }
                else if (pawn.HostFaction == Faction.OfPlayer)
                {
                    sb.Append("访客 ");
                }
                else if (pawn.Faction != null && pawn.Faction != Faction.OfPlayer)
                {
                    sb.Append(pawn.Faction.Name);
                    sb.Append(" ");
                }

                // 5. 特性（所有特性）
                if (pawn.story?.traits != null)
                {
                    foreach (var trait in pawn.story.traits.allTraits)
                    {
                        if (trait?.def?.label != null)
                        {
                            sb.Append(trait.def.label);
                            sb.Append(" ");
                        }
                    }
                }

                // 6. 技能（所有技能，带等级）
                if (pawn.skills != null)
                {
                    foreach (var skillRecord in pawn.skills.skills)
                    {
                        if (skillRecord.TotallyDisabled || skillRecord.def?.label == null)
                            continue;
                        
                        int level = skillRecord.Level;
                        
                        // 只输出有一定等级的技能（>=5级）
                        if (level >= 5)
                        {
                            sb.Append(skillRecord.def.label);
                            sb.Append(level);
                            sb.Append(" ");
                            
                            // 高等级技能额外标记
                            if (level >= 15)
                            {
                                sb.Append(skillRecord.def.label);
                                sb.Append("精通 ");
                            }
                            else if (level >= 10)
                            {
                                sb.Append(skillRecord.def.label);
                                sb.Append("熟练 ");
                            }
                        }
                    }
                }

                // 7. 健康状况
                if (pawn.health != null)
                {
                    if (pawn.health.hediffSet.GetInjuredParts().Any())
                    {
                        sb.Append("受伤 ");
                    }
                    else if (!pawn.health.HasHediffsNeedingTend())
                    {
                        sb.Append("健康 ");
                    }
                }

                // 8. 关系（前5个相关Pawn）
                if (pawn.relations != null)
                {
                    var relatedPawns = pawn.relations.RelatedPawns.Take(5);
                    foreach (var relatedPawn in relatedPawns)
                    {
                        if (!string.IsNullOrEmpty(relatedPawn.Name?.ToStringShort))
                        {
                            sb.Append(relatedPawn.Name.ToStringShort);
                            sb.Append(" ");
                        }
                    }
                }

                // 9. 成年背景（使用完整标题）
                if (pawn.story?.Adulthood != null)
                {
                    string backstoryTitle = pawn.story.Adulthood.TitleFor(pawn.gender);
                    if (!string.IsNullOrEmpty(backstoryTitle))
                    {
                        sb.Append(backstoryTitle);
                        sb.Append(" ");
                    }
                }
                
                // 10. 童年背景（使用完整标题）
                if (pawn.story?.Childhood != null)
                {
                    string childhoodTitle = pawn.story.Childhood.TitleFor(pawn.gender);
                    if (!string.IsNullOrEmpty(childhoodTitle))
                    {
                        sb.Append(childhoodTitle);
                        sb.Append(" ");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[标签测试] 构建Pawn信息时出错: {ex.Message}");
            }

            return sb.ToString().Trim();
        }

        private bool TestTagMatch(string text, CommonKnowledgeEntry entry)
        {
            var tags = entry.GetTags();
            if (tags == null || tags.Count == 0) return false;

            switch (entry.matchMode)
            {
                case KeywordMatchMode.Any:
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        if (text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }
                    return false;

                case KeywordMatchMode.All:
                    foreach (var tag in tags)
                    {
                        if (string.IsNullOrWhiteSpace(tag)) continue;
                        if (text.IndexOf(tag, StringComparison.OrdinalIgnoreCase) < 0)
                            return false;
                    }
                    return true;

                default:
                    return false;
            }
        }
    }
}
