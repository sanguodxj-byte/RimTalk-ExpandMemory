using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;

namespace RimTalk.Memory
{
    /// <summary>
    /// 关键词提取辅助类
    /// ★ v3.3.20: 拆分复杂的Pawn关键词提取逻辑
    /// </summary>
    public static class KeywordExtractionHelper
    {
        /// <summary>
        /// 提取角色关键词（带详细信息）
        /// </summary>
        public static PawnKeywordInfo ExtractPawnKeywords(List<string> keywords, Verse.Pawn pawn)
        {
            var info = new PawnKeywordInfo
            {
                PawnName = pawn?.LabelShort ?? "Unknown"
            };
            
            if (pawn == null || keywords == null)
                return info;

            try
            {
                // 1. 名字
                ExtractNameKeywords(pawn, keywords, info);
                
                // 2. 年龄段
                ExtractAgeKeywords(pawn, keywords, info);
                
                // 3. 性别
                ExtractGenderKeywords(pawn, keywords, info);
                
                // 4. 种族
                ExtractRaceKeywords(pawn, keywords, info);
                
                // 4.5. 身份
                ExtractIdentityKeywords(pawn, keywords, info);
                
                // 5. 特质
                ExtractTraitKeywords(pawn, keywords, info);
                
                // 6. 技能
                ExtractSkillKeywords(pawn, keywords, info);
                
                // 7. 健康状况
                ExtractHealthKeywords(pawn, keywords, info);
                
                // 8. 关系
                ExtractRelationshipKeywords(pawn, keywords, info);
                
                // 9-10. 背景
                ExtractBackstoryKeywords(pawn, keywords, info);

                info.TotalCount = info.NameKeywords.Count + info.AgeKeywords.Count + info.GenderKeywords.Count + 
                                 info.RaceKeywords.Count + info.TraitKeywords.Count + info.SkillKeywords.Count + 
                                 info.SkillLevelKeywords.Count + info.HealthKeywords.Count + 
                                 info.RelationshipKeywords.Count + info.BackstoryKeywords.Count + info.ChildhoodKeywords.Count;
            }
            catch (Exception ex)
            {
                Log.Error($"[KeywordExtraction] Error extracting pawn keywords: {ex.Message}");
            }
            
            return info;
        }
        
        // ==================== 私有提取方法 ====================
        
        private static void ExtractNameKeywords(Verse.Pawn pawn, List<string> keywords, PawnKeywordInfo info)
        {
            if (!string.IsNullOrEmpty(pawn.Name?.ToStringShort))
            {
                var name = pawn.Name.ToStringShort;
                AddAndRecord(name, keywords, info.NameKeywords);
            }
        }
        
        private static void ExtractAgeKeywords(Verse.Pawn pawn, List<string> keywords, PawnKeywordInfo info)
        {
            if (pawn.RaceProps != null && pawn.RaceProps.Humanlike)
            {
                float ageYears = pawn.ageTracker.AgeBiologicalYearsFloat;
                
                if (ageYears < 3f)
                {
                    AddAndRecord("婴儿", keywords, info.AgeKeywords);
                    AddAndRecord("宝宝", keywords, info.AgeKeywords);
                }
                else if (ageYears < 13f)
                {
                    AddAndRecord("儿童", keywords, info.AgeKeywords);
                    AddAndRecord("小孩", keywords, info.AgeKeywords);
                }
                else if (ageYears < 18f)
                {
                    AddAndRecord("青少年", keywords, info.AgeKeywords);
                }
                else
                {
                    AddAndRecord("成人", keywords, info.AgeKeywords);
                }
            }
        }
        
        private static void ExtractGenderKeywords(Verse.Pawn pawn, List<string> keywords, PawnKeywordInfo info)
        {
            if (pawn.gender != null)
            {
                var genderLabel = pawn.gender.GetLabel();
                AddAndRecord(genderLabel, keywords, info.GenderKeywords);
            }
        }
        
        private static void ExtractRaceKeywords(Verse.Pawn pawn, List<string> keywords, PawnKeywordInfo info)
        {
            if (pawn.def != null)
            {
                AddAndRecord(pawn.def.label, keywords, info.RaceKeywords);
                
                // 亚种信息（Biotech DLC）
                try
                {
                    if (pawn.genes != null && pawn.genes.Xenotype != null)
                    {
                        string xenotypeName = pawn.genes.Xenotype.label ?? pawn.genes.Xenotype.defName;
                        if (!string.IsNullOrEmpty(xenotypeName))
                        {
                            AddAndRecord(xenotypeName, keywords, info.RaceKeywords);
                        }
                    }
                }
                catch { /* 兼容性：没有Biotech DLC时跳过 */ }
            }
        }
        
        private static void ExtractIdentityKeywords(Verse.Pawn pawn, List<string> keywords, PawnKeywordInfo info)
        {
            if (pawn.IsColonist)
            {
                AddAndRecord("殖民者", keywords, info.IdentityKeywords);
            }
            else if (pawn.IsPrisoner)
            {
                AddAndRecord("囚犯", keywords, info.IdentityKeywords);
            }
            else if (pawn.IsSlaveOfColony)
            {
                AddAndRecord("奴隶", keywords, info.IdentityKeywords);
            }
            else if (pawn.HostFaction == Faction.OfPlayer)
            {
                AddAndRecord("访客", keywords, info.IdentityKeywords);
            }
            else if (pawn.Faction != null && pawn.Faction != Faction.OfPlayer)
            {
                AddAndRecord(pawn.Faction.Name, keywords, info.IdentityKeywords);
            }
        }
        
        private static void ExtractTraitKeywords(Verse.Pawn pawn, List<string> keywords, PawnKeywordInfo info)
        {
            if (pawn.story?.traits != null)
            {
                foreach (var trait in pawn.story.traits.allTraits)
                {
                    if (trait?.def?.label != null)
                    {
                        AddAndRecord(trait.def.label, keywords, info.TraitKeywords);
                    }
                }
            }
        }
        
        private static void ExtractSkillKeywords(Verse.Pawn pawn, List<string> keywords, PawnKeywordInfo info)
        {
            if (pawn.skills != null)
            {
                foreach (var skillRecord in pawn.skills.skills)
                {
                    if (skillRecord.TotallyDisabled || skillRecord.def?.label == null)
                        continue;
                    
                    int level = skillRecord.Level;
                    
                    // 只提取有一定等级的技能（>=5级）
                    if (level >= 5)
                    {
                        // 添加技能名
                        AddAndRecord(skillRecord.def.label, keywords, info.SkillKeywords);
                        
                        // 添加技能名+等级
                        string skillWithLevel = skillRecord.def.label + level;
                        AddAndRecord(skillWithLevel, keywords, info.SkillKeywords);
                        
                        // 添加等级标记
                        if (level >= 15)
                        {
                            AddAndRecord(skillRecord.def.label + "精通", keywords, info.SkillLevelKeywords);
                        }
                        else if (level >= 10)
                        {
                            AddAndRecord(skillRecord.def.label + "熟练", keywords, info.SkillLevelKeywords);
                        }
                    }
                }
            }
        }
        
        private static void ExtractHealthKeywords(Verse.Pawn pawn, List<string> keywords, PawnKeywordInfo info)
        {
            if (pawn.health != null)
            {
                if (pawn.health.hediffSet.GetInjuredParts().Any())
                {
                    AddAndRecord("受伤", keywords, info.HealthKeywords);
                }
                else if (!pawn.health.HasHediffsNeedingTend())
                {
                    AddAndRecord("健康", keywords, info.HealthKeywords);
                }
            }
        }
        
        private static void ExtractRelationshipKeywords(Verse.Pawn pawn, List<string> keywords, PawnKeywordInfo info)
        {
            if (pawn.relations != null)
            {
                var relatedPawns = pawn.relations.RelatedPawns.Take(5);
                foreach (var relatedPawn in relatedPawns)
                {
                    if (!string.IsNullOrEmpty(relatedPawn.Name?.ToStringShort))
                    {
                        AddAndRecord(relatedPawn.Name.ToStringShort, keywords, info.RelationshipKeywords);
                    }
                }
            }
        }
        
        private static void ExtractBackstoryKeywords(Verse.Pawn pawn, List<string> keywords, PawnKeywordInfo info)
        {
            // 使用完整标题而不是缩写
            if (pawn.story?.Adulthood != null)
            {
                string backstoryTitle = pawn.story.Adulthood.TitleFor(pawn.gender);
                if (!string.IsNullOrEmpty(backstoryTitle))
                {
                    AddAndRecord(backstoryTitle, keywords, info.BackstoryKeywords);
                }
            }
            
            if (pawn.story?.Childhood != null)
            {
                string childhoodTitle = pawn.story.Childhood.TitleFor(pawn.gender);
                if (!string.IsNullOrEmpty(childhoodTitle))
                {
                    AddAndRecord(childhoodTitle, keywords, info.ChildhoodKeywords);
                }
            }
        }
        
        /// <summary>
        /// 添加关键词并记录（避免重复）
        /// </summary>
        private static void AddAndRecord(string keyword, List<string> allKeywords, List<string> categoryKeywords)
        {
            if (string.IsNullOrEmpty(keyword))
                return;
            
            if (!allKeywords.Contains(keyword))
            {
                allKeywords.Add(keyword);
            }
            if (!categoryKeywords.Contains(keyword))
            {
                categoryKeywords.Add(keyword);
            }
        }
    }
}
