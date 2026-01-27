using RimTalk.Data;
using RimTalk.Memory;
using RimTalk.Source.Data;
using RimTalk.Util;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Cache = RimTalk.Data.Cache;

namespace RimTalkHistoryPlus
{
    public class History : MemoryEntry, IExposable //ILoadReferenceable 暂时用不到引用保存的接口 // 继承为MemoryEntry的子类以便接入记忆拓展
    {
        public long HistoryUniqueID = -1;
        // 时间
        public int GameTick = -1; // 这个其实没用了，但考虑兼容记忆拓展先留着
        public long AbsTick = -1;
        // 地点
        private PlanetTile _planetTile = PlanetTile.Invalid; // 这个字段以及一些其他字段其实都可以甚至应该作为MemoryEntry的字段
        private int _mapId = -1; // 总之先留着
        public bool IsHomeMap = false;
        // 人物
        public HashSet<Pawn> Pawns = new();

        // 对话内容
        // public string TextBlock; 改用 MemoryEntry 自带的 content 字段

        public History() { }
        public History(List<TalkResponse> responses) : base(
            content: string.Empty,
            type: MemoryType.Conversation,
            layer: MemoryLayer.Active,
            importance: 0.5f
            )
        {
            if (responses == null || responses.Count == 0)
            {
                Log.Warning("[RimTalkHistoryPlus] Attempted to Create History with null.");
                return;
            }

            // 创建文本
            var modifiedText = ModifyResponses(responses);
            if (string.IsNullOrWhiteSpace(modifiedText))
            {
                Log.Warning("[RimTalkHistoryPlus] Attempted to Create History with empty TextBlock.");
                return;
            }
            var maxTextBlockLength = HistoryManager.MaxTextBlockLength;
            if (modifiedText.Length > maxTextBlockLength)
            {
                modifiedText = modifiedText.Substring(0, maxTextBlockLength) + "...";
                Log.Warning($"[RimTalkHistoryPlus] 历史字数超出{maxTextBlockLength}，已截短");
            }
            // 如果为玩家发起的对话，则插入玩家发言
            if (responses.First()?.TalkType == TalkType.User) // 这步判断需要抽出TalkType，可能可以优化
            {
                modifiedText = $"{HistoryManager.Instance?.PlayerDialogue}\n{modifiedText}";
                Log.Message("[RimTalkHistoryPlus] 成功插入玩家文本");
            }
            content = modifiedText;

            // 创建参与者集合
            Pawns = responses
                .Select(r => Cache.GetByName(r.Name)?.Pawn)
                .Where(p => p != null)
                .ToHashSet();

            // 初始化其余字段
            HistoryUniqueID = HistoryManager.GetNewHistoryId();
            GameTick = (Find.TickManager != null) ? Find.TickManager.TicksGame : -1;
            AbsTick = (Find.TickManager != null) ? Find.TickManager.TicksAbs : -1;

            // 因为RimTalk的问题，这里获取地点的方式很蛋疼，没辙，期待后续优化吧
            if (Pawns.Count == 0)
            {
                Log.Error("[RimTalkHistoryPlus] 创建历史时发现不存在对话参与者，这应当是不可能的.");
                return;
            }
            _planetTile = Pawns.First().Tile;
            Map map = null;
            foreach (var pawn in Pawns)
            {
                map = pawn.Map;
                if (map != null) break;
            }
            _mapId = map != null ? map.uniqueID : -1;
            IsHomeMap = map != null && map.IsPlayerHome;

            // Log.Message($"[RimTalkHistoryPlus] Created History: pawns={Pawns.Count}, tick={AbsTick}, IsHomeMap={IsHomeMap}, MapId={_mapId}");
        }

        // 计算并返回历史的日期时间字符串
        public string GetDateAndTime()
        {
            // 若信息缺失，返回未知
            if (!_planetTile.Valid || AbsTick == -1) return "Unknown Date";

            // 保护性检查：WorldGrid 访问可能抛出异常，捕获并返回 Unknown Date
            try
            {
                // 使用本体方法得到日期，RimTalk方法得到时间
                var location = Find.WorldGrid.LongLatOf(_planetTile);
                return $"{GenDate.DateFullStringAt(AbsTick, location)} {CommonUtil.GetInGameHour12HString(AbsTick, location)}";
            }
            catch
            {
                Log.Warning($"[RimTalkHistoryPlus] 时间计算异常");
                return "Unknown Date";
            }
        }

        // 返回历史参与者名单，逗号分隔
        public string GetParticipants()
        {
            if (Pawns == null || Pawns.Count == 0) return string.Empty;
            Pawns.RemoveWhere(p => p == null); // 清理 null 项，顺手的事
            var names = Pawns
                .Select(p => p.LabelShort ?? string.Empty);
            return string.Join(", ", names);
        }

        // 将原始 response 文本解析并清理为一段或多段文本（用换行分隔）
        // 这两个方法都应该放在 RimTalk 的 Util 里才对，或者我单开一个 Util，但这里我懒得搞，先放着
        private static string ModifyResponses(List<TalkResponse> responses)
        {
            var lines = new List<string>();
            foreach (var response in responses)
            {
                if (response == null) continue;

                var text = CleanText(response.Text);
                if (string.IsNullOrWhiteSpace(text)) continue;

                var name = CleanText(response.Name);

                lines.Add(string.IsNullOrWhiteSpace(name) ? text : $"{name}: {text}");
            }
            return string.Join("\n", lines);
        }
        private static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            return CommonUtil.StripFormattingTags(text).Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
        }

        public new void ExposeData()
        {
            base.ExposeData();

            // 保存/加载字段
            Scribe_Values.Look(ref HistoryUniqueID, "HistoryUniqueID", -1);

            Scribe_Values.Look(ref GameTick, "GameTick", -1);
            Scribe_Values.Look(ref AbsTick, "AbsTick", -1);

            Scribe_Values.Look(ref _planetTile, "Tile", PlanetTile.Invalid);
            Scribe_Values.Look(ref _mapId, "MapId", -1);
            Scribe_Values.Look(ref IsHomeMap, "IsHomeMap", false);

            Scribe_Collections.Look(ref Pawns, "Pawns", LookMode.Reference);

            // Scribe_Values.Look(ref TextBlock, "TextBlock", string.Empty);

            // 确保集合不为 null
            if (Pawns == null)
            {
                Log.Warning($"[RimTalkHistoryPlus] ExposeData for History: tick={AbsTick}时发现其Pawns为空");
                Pawns = new HashSet<Pawn>();
            }

            // Log.Message($"[RimTalkHistoryPlus] ExposeData for History: tick={AbsTick}");
        }

        public string GetUniqueLoadID()
        {
            return $"RimTalkHistoryPlus_History_{HistoryUniqueID}"; // 考虑进一步唯一化？
        }
    }
}
