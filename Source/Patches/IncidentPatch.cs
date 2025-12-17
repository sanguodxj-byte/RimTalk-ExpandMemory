using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;
using RimWorld;

using RimTalk.MemoryPatch;
using RimTalk.Memory;

namespace RimTalk.Memory.Patches
{
    /// <summary>
    /// ������Ϸ�¼���Incident��ϵͳ��ʵʱ������Ҫ�¼�
    /// ? ֧�����׶��¼���¼��Ϯ������ �� ���˸���
    /// </summary>
    [HarmonyPatch(typeof(IncidentWorker), nameof(IncidentWorker.TryExecute))]
    public static class IncidentPatch
    {
        // ? ׷�ٻ�Ծ��Ϯ���¼������ں������£�
        private static Dictionary<int, RaidEventInfo> activeRaids = new Dictionary<int, RaidEventInfo>();
        
        [HarmonyPostfix]
        public static void Postfix(IncidentWorker __instance, IncidentParms parms, bool __result)
        {
            // ֻ����ɹ�ִ�е��¼�
            if (!__result)
                return;
            
            // ��������Ƿ�����
            if (!RimTalkMemoryPatchMod.Settings.enableEventRecordKnowledge)
                return;
            
            try
            {
                var incidentDef = __instance.def;
                if (incidentDef == null)
                    return;
                
                // ? ���⴦��Ϯ���¼�
                if (IsRaidIncident(incidentDef))
                {
                    HandleRaidStart(incidentDef, parms);
                    return;
                }
                
                // �����¼����ͺ���Ҫ��
                float importance = CalculateIncidentImportance(incidentDef);
                
                // ֻ��¼��Ҫ�¼�
                if (importance < 0.5f)
                    return;
                
                // �����¼�����
                string eventText = GenerateEventDescription(incidentDef, parms);
                
                if (string.IsNullOrEmpty(eventText))
                    return;
                
                // ��ӵ���ʶ��
                AddOrUpdateKnowledge(null, eventText, importance);
            }
            catch (Exception ex)
            {
                Log.Error($"[EventRecord] Error in IncidentPatch: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ? �ж��Ƿ���Ϯ���¼�
        /// </summary>
        private static bool IsRaidIncident(IncidentDef incidentDef)
        {
            string defName = incidentDef.defName;
            return defName.Contains("Raid") || 
                   defName.Contains("Siege") || 
                   defName.Contains("Mech") && defName.Contains("Cluster") ||
                   incidentDef.category == IncidentCategoryDefOf.ThreatBig;
        }
        
        /// <summary>
        /// ? ����Ϯ����ʼ
        /// </summary>
        private static void HandleRaidStart(IncidentDef incidentDef, IncidentParms parms)
        {
            // ����Ϯ��ID�����ں������£�
            int raidId = GenTicks.TicksGame;
            
            // ��ȡ��ϵ��Ϣ
            string factionName = "δ֪����";
            if (parms.faction != null && !string.IsNullOrEmpty(parms.faction.Name))
            {
                factionName = parms.faction.Name;
            }
            
            // ��ȡϮ������
            string raidType = GetRaidType(incidentDef);
            
            // ���ɳ�ʼ����
            string eventText = $"����{factionName}������{raidType}";
            
            // ��ӵ���ʶ��
            var entry = AddOrUpdateKnowledge(null, eventText, 0.9f);
            
            // ��¼��ԾϮ����Ϣ
            if (entry != null)
            {
                activeRaids[raidId] = new RaidEventInfo
                {
                    entryId = entry.id,
                    factionName = factionName,
                    raidType = raidType,
                    startTick = GenTicks.TicksGame,
                    initialText = eventText
                };
                
                // ������������Ϯ��������
                if (!raidCheckActive)
                {
                    raidCheckActive = true;
                }
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[EventRecord] ?? Raid started: {eventText} (ID: {raidId})");
                }
            }
        }
        
        /// <summary>
        /// ? ��ȡϮ������
        /// </summary>
        private static string GetRaidType(IncidentDef incidentDef)
        {
            string defName = incidentDef.defName;
            
            if (defName.Contains("Siege"))
                return "Χ��";
            else if (defName.Contains("Mech"))
                return "��е�幥��";
            else if (defName.Contains("Sapper"))
                return "����Ϯ��";
            else if (defName.Contains("Breacher"))
                return "�ƻ���Ϯ��";
            else
                return "Ϯ��";
        }
        
        /// <summary>
        /// ? ���Ϯ��״̬��ÿСʱ����һ�Σ�
        /// </summary>
        private static bool raidCheckActive = false;
        
        public static void CheckRaidStatus()
        {
            if (!raidCheckActive || activeRaids.Count == 0)
                return;
            
            try
            {
                var library = MemoryManager.GetCommonKnowledge();
                if (library == null)
                    return;
                
                int currentTick = GenTicks.TicksGame;
                var completedRaids = new List<int>();
                
                foreach (var kvp in activeRaids)
                {
                    int raidId = kvp.Key;
                    var raidInfo = kvp.Value;
                    
                    // ����Ƿ�ʱ������4Сʱ��Ϊ������
                    int elapsedTicks = currentTick - raidInfo.startTick;
                    if (elapsedTicks > 10000) // 4Сʱ = 2500 * 4
                    {
                        // ��ʱ���ж�Ϊ����
                        UpdateRaidOutcome(library, raidInfo, true);
                        completedRaids.Add(raidId);
                    }
                    else
                    {
                        // ����ͼ���Ƿ��е���
                        bool hasEnemies = CheckForEnemies();
                        
                        if (!hasEnemies && elapsedTicks > 1000) // ���ٳ���һ��ʱ��������
                        {
                            // ���˳ɹ�
                            UpdateRaidOutcome(library, raidInfo, true);
                            completedRaids.Add(raidId);
                        }
                    }
                }
                
                // ��������ɵ�Ϯ��
                foreach (var raidId in completedRaids)
                {
                    activeRaids.Remove(raidId);
                }
                
                // ���û�л�ԾϮ����ֹͣ���
                if (activeRaids.Count == 0)
                {
                    raidCheckActive = false;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[EventRecord] Error checking raid status: {ex.Message}");
            }
        }
        
        /// <summary>
        /// ? ����ͼ���Ƿ��еж�����
        /// </summary>
        private static bool CheckForEnemies()
        {
            if (Find.CurrentMap == null)
                return false;
            
            foreach (var pawn in Find.CurrentMap.mapPawns.AllPawnsSpawned)
            {
                if (pawn.HostileTo(Faction.OfPlayer) && !pawn.Dead && !pawn.Downed)
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// ? ����Ϯ�����
        /// </summary>
        private static void UpdateRaidOutcome(CommonKnowledgeLibrary library, RaidEventInfo raidInfo, bool defeated)
        {
            // ����ԭʼ��Ŀ
            var entry = library.Entries.FirstOrDefault(e => e.id == raidInfo.entryId);
            
            if (entry == null)
            {
                // ��Ŀ��ɾ���ˣ�ֱ�ӷ���
                return;
            }
            
            // ��������
            if (defeated)
            {
                entry.content = $"{raidInfo.initialText}��ֳ��سɹ������˽���";
                entry.importance = 0.95f; // �����Ҫ��
            }
            else
            {
                entry.content = $"{raidInfo.initialText}�������������ʧ";
                entry.importance = 1.0f; // �����Ҫ��
            }
            
            if (Prefs.DevMode)
            {
                Log.Message($"[EventRecord] ? Updated raid outcome: {entry.content}");
            }
        }
        
        /// <summary>
        /// ��ӻ���³�ʶ
        /// </summary>
        private static CommonKnowledgeEntry AddOrUpdateKnowledge(string existingId, string eventText, float importance)
        {
            var library = MemoryManager.GetCommonKnowledge();
            if (library == null)
                return null;
            
            CommonKnowledgeEntry entry = null;
            
            // ����ṩ��ID�����Ը���������Ŀ
            if (!string.IsNullOrEmpty(existingId))
            {
                entry = library.Entries.FirstOrDefault(e => e.id == existingId);
                if (entry != null)
                {
                    entry.content = eventText;
                    entry.importance = importance;
                    return entry;
                }
            }
            
            // ����Ƿ��Ѵ�����������
            bool exists = library.Entries.Any(e => 
                e.content.Contains(eventText.Substring(0, Math.Min(15, eventText.Length)))
            );
            
            if (!exists)
            {
                entry = new CommonKnowledgeEntry("�¼�,��ʷ", eventText)
                {
                    importance = importance,
                    isEnabled = true,
                    isUserEdited = false
                };
                
                library.AddEntry(entry);
                
                if (Prefs.DevMode)
                {
                    Log.Message($"[EventRecord] ? Created knowledge: {eventText} (importance: {importance:F2})");
                }
            }
            
            return entry;
        }
        
        /// <summary>
        /// �����¼���Ҫ��
        /// </summary>
        private static float CalculateIncidentImportance(IncidentDef incidentDef)
        {
            string defName = incidentDef.defName;
            string label = incidentDef.label;
            
            // Ϯ�������HandleRaidStart�д�������ﲻ���ж�
            
            // ������أ�����Ҫ1.0��
            if (defName.Contains("Death") || defName.Contains("Dead") || 
                label.Contains("��") || label.Contains("death"))
                return 1.0f;
            
            // ��ϵ�仯����Ҫ��0.85��
            if (defName.Contains("Marriage") || defName.Contains("Wedding") || 
                label.Contains("���") || label.Contains("��"))
                return 0.85f;
            
            // ? ������������أ���Ҫ��0.9��
            if (defName.Contains("Funeral") || defName.Contains("Burial") || 
                label.Contains("����") || label.Contains("��") || label.Contains("����"))
                return 0.9f;
            
            // ? ������������أ���Ҫ��0.7��
            if (defName.Contains("Birthday") || label.Contains("����"))
                return 0.7f;
            
            // ? �������о�ͻ�ƣ���Ҫ��0.8��
            if (defName.Contains("Breakthrough") || defName.Contains("Research") && defName.Contains("Complete") ||
                label.Contains("ͻ��") || label.Contains("����о�"))
                return 0.8f;
            
            // ? ��������������Ҫ��0.7��
            if (defName.Contains("Anniversary") || label.Contains("����"))
                return 0.7f;
            
            // ��Ա�䶯����Ҫ��0.8��
            if (defName.Contains("Join") || defName.Contains("Refugee") || 
                defName.Contains("WandererJoin") || 
                label.Contains("����") || label.Contains("����"))
                return 0.8f;
            
            // ����
            if (defName.Contains("Infestation") || label.Contains("��"))
                return 0.85f;
            
            // ���ѣ���Ҫ��0.85��
            if (defName.Contains("Fire") || defName.Contains("Explosion") || 
                defName.Contains("Tornado") || defName.Contains("Eclipse") ||
                label.Contains("��") || label.Contains("��ը") || label.Contains("�����"))
                return 0.85f;
            
            // ó��/�ÿͣ���Ҫ��0.6��
            if (defName.Contains("Caravan") || defName.Contains("Visitor") || 
                defName.Contains("Trade") ||
                label.Contains("ó��") || label.Contains("�ÿ�"))
                return 0.6f;
            
            // ��������Ҫ��0.7��
            if (defName.Contains("Disease") || label.Contains("����") || label.Contains("����"))
                return 0.75f;
            
            // ������ɣ���Ҫ��0.65��
            if (defName.Contains("Quest") || label.Contains("����"))
                return 0.65f;
            
            // ���������ȼ��¼�
            return 0.3f;
        }
        
        /// <summary>
        /// �����¼���������Ϯ���¼���
        /// </summary>
        private static string GenerateEventDescription(IncidentDef incidentDef, IncidentParms parms)
        {
            string label = incidentDef.label;
            string defName = incidentDef.defName;
            
            // ���ʱ��ǰ׺
            string timePrefix = "����";
            
            // ���������¼�����
            if (defName.Contains("Marriage") || defName.Contains("Wedding"))
            {
                return $"{timePrefix}�����˻���";
            }
            else if (defName.Contains("Funeral") || defName.Contains("Burial"))
            {
                return $"{timePrefix}����������";
            }
            else if (defName.Contains("Birthday"))
            {
                return $"{timePrefix}��ף������";
            }
            else if (defName.Contains("Breakthrough") || defName.Contains("Research") && defName.Contains("Complete"))
            {
                return $"{timePrefix}ȡ�����о�ͻ��";
            }
            else if (defName.Contains("Anniversary"))
            {
                return $"{timePrefix}��ף���������";
            }
            else if (defName.Contains("WandererJoin") || defName.Contains("RefugeeJoin"))
            {
                return $"{timePrefix}���³�Ա����ֳ���";
            }
            else if (defName.Contains("Infestation"))
            {
                return $"{timePrefix}�����˳�������";
            }
            else if (defName.Contains("Fire"))
            {
                return $"{timePrefix}�����˻���";
            }
            else if (defName.Contains("Explosion"))
            {
                return $"{timePrefix}�����˱�ը";
            }
            else if (defName.Contains("Tornado"))
            {
                return $"{timePrefix}�����������";
            }
            else if (defName.Contains("Eclipse"))
            {
                return $"{timePrefix}��������ʳ";
            }
            else if (defName.Contains("TraderCaravan") || defName.Contains("VisitorGroup"))
            {
                // ó��/�ÿ�ͨ������¼
                return null;
            }
            
            // ͨ��������ʹ����Ϸ���ػ���label
            if (!string.IsNullOrEmpty(label))
            {
                return $"{timePrefix}{label}";
            }
            
            return null;
        }
        
        /// <summary>
        /// ? Ϯ���¼���Ϣ
        /// </summary>
        private class RaidEventInfo
        {
            public string entryId;          // ��ʶ��ĿID
            public string factionName;      // ��ϵ����
            public string raidType;         // Ϯ������
            public int startTick;           // ��ʼʱ��
            public string initialText;      // ��ʼ����
        }
    }
}
