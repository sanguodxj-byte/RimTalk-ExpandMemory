using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace RimTalk.Memory
{

    // 轮次记忆
    // 表面是Pawn的记忆（继承MemoryEntry），实际是编剧（LLM）的记忆
    public class RoundMemory : MemoryEntry, IExposable
    {
        // 唯一编号
        public long RoundMemoryUniqueID = -1;
        // 时间
        private long AbsTick = -1;
        // 地点
        private PlanetTile planetTile = PlanetTile.Invalid;
        public bool IsHomeMap = false;
        // 人物
        public HashSet<Pawn> Pawns = new();

        public RoundMemory() { }
        public RoundMemory(HashSet<Pawn> pawns, string content) : base(
            content: string.Empty,
            type: MemoryType.Conversation,
            layer: MemoryLayer.Active,
            importance: 0.5f
            )
        {
            // 构建参与者集合，可能为空集合
            Pawns = pawns ?? new();
            Pawns.RemoveWhere(p => p is null);

            // 构建文本
            // 截短超限文本
            var maxContentLength = RoundMemoryManager.MaxContentLength;
            if (content.Length > maxContentLength)
            {
                content = content.Substring(0, maxContentLength) + "...";
                Log.Warning($"[RoundMemory] RoundMemory字数超出{maxContentLength}，已截短");
            }
            // 显式显示参与者名单，完成文本构建
            this.content = $"[对话参与者: {GetParticipants()}]\n{content}";

            // 构建唯一ID和时间
            RoundMemoryUniqueID = RoundMemoryManager.GetNewRoundMemoryId();
            AbsTick = Find.TickManager?.TicksAbs ?? -1;

            // 构建地点
            // 因为RimTalk的问题，这里获取地点的方式很蛋疼，没辙
            if (Pawns.Count == 0)
            {
                Log.Warning("[RoundMemory] 创建RoundMemory时发现不存在对话参与者");
                return;
            }
            planetTile = Pawns.FirstOrDefault()?.Tile ?? PlanetTile.Invalid;
            IsHomeMap = Pawns.Select(p => p.Map).FirstOrDefault(m => m is not null)?.IsPlayerHome ?? false;
        }

        // 计算并返回历史的日期时间字符串
        public string GetDateAndTime()
        {
            // 若信息缺失，返回未知
            if (!planetTile.Valid || AbsTick == -1) return "Unknown Date";

            // 若位置获取异常，则使用默认值
            var location = Find.WorldGrid?.LongLatOf(planetTile) ?? Vector2.zero;

            // 使用本体方法得到日期，RimTalk方法得到时间
            return $"{GenDate.DateFullStringAt(AbsTick, location)} {GetInGameHour12HString(AbsTick, location)}";
        }

        // 考虑未来将此方法放在一个公共工具类中
        private static string GetInGameHour12HString(long absTicks, Vector2 longLat)
        {
            int hour24 = GenDate.HourOfDay(absTicks, longLat.x);
            int hour12 = hour24 % 12;
            if (hour12 == 0) hour12 = 12;
            string arg = (hour24 < 12) ? "am" : "pm";
            return $"{hour12}{arg}";
        }

        // 返回历史参与者名单，逗号分隔
        public string GetParticipants()
        {
            return string.Join(", ", Pawns
                .Select(p => p?.LabelShort)
                .Where(n => n is not null)
            );
        }

        // 存档读写
        public override void ExposeData()
        {
            base.ExposeData();

            // 保存/加载字段
            Scribe_Values.Look(ref RoundMemoryUniqueID, "RoundMemoryUniqueID", -1);

            Scribe_Values.Look(ref AbsTick, "AbsTick", -1);

            Scribe_Values.Look(ref planetTile, "Tile", PlanetTile.Invalid);
            Scribe_Values.Look(ref IsHomeMap, "IsHomeMap", false);

            Scribe_Collections.Look(ref Pawns, "Pawns", LookMode.Reference);

            // 确保集合不为 null
            if (Pawns is null)
            {
                Log.Warning($"[RoundMemory] ExposeData for RoundMemory: tick={AbsTick}时发现其Pawns为空");
                Pawns = new();
            }
            // 清理Pawns中的null条目
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                Pawns.RemoveWhere(p => p is null);
            }
        }

        public string GetUniqueLoadID()
        {
            return $"RoundMemory_{RoundMemoryUniqueID}"; // 考虑进一步唯一化？
        }
    }

}
