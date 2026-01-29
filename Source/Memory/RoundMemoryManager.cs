using RimTalk.Data;
using RimTalk.Memory;
using RimTalk.MemoryPatch;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.Memory
{
    /// <summary>
    /// ⭐ v4.0.1: 重构后的 RoundMemoryManager
    ///
    /// 职责简化：
    /// - ID 发号机（为 RoundMemory 分配唯一 ID）
    /// - 去重缓存（防止同一对话被重复注入）
    /// - 玩家对话缓存（临时存储玩家输入）
    ///
    /// 已移除：
    /// - _roundMemories 全局列表（RoundMemory 现在只存储在 FourLayerMemoryComp.ABM 中）
    /// - InjectRoundMemory 方法（被 InjectABM 替代）
    /// - FinalizeInit 指针修正（不再需要双重存储）
    /// </summary>
    public class RoundMemoryManager : GameComponent
    {
        // Manager实例，供静态方法访问，仅在存档内有效
        private static RoundMemoryManager _instance;
        public static RoundMemoryManager Instance
        {
            get
            {
                if (Current.Game == null)
                {
                    Log.Warning("[RoundMemory] RoundMemory中控台仅在存档内有效");
                    return null;
                }
                return _instance;
            }
        }

        // 玩家对话缓存
        public Pawn Player; // 调用时为空是正常的，需要在调用端进行空值检查
        private string _playerDialogue = string.Empty;
        public string PlayerDialogue
        {
            get => _playerDialogue;
            set => _playerDialogue = value;
        }

        // 查重缓存（用于 InjectABM 跨 Pawn 去重）
        public int LastContextTick = -1;
        private const int CONTEXT_EXPIRE_TICKS = 120;
        private HashSet<RoundMemory> _roundMemoryCache;
        public HashSet<RoundMemory> RoundMemoryCache
        {
            get
            {
                if (_roundMemoryCache == null)
                {
                    _roundMemoryCache = new();
                }
                return _roundMemoryCache;
            }
            set => _roundMemoryCache = value;
        }

        // 发号机
        public long _nextRoundMemoryId = 0;

        // 配置常量
        public static bool DevSwitch = false;
        public static int MaxTextBlockLength = 10000; // 创建时单条RoundMemory最大文本长度
        public static int MaxTextBlockInjectedLength = 5000; // 注入时单条RoundMemory最大文本长度
        public static int MaxInjectedLength = 20000; // 注入时最大总文本长度

        public static bool IsPlayerDialogueInject => RimTalkMemoryPatchMod.Settings?.IsPlayerDialogueInject ?? true;

        public RoundMemoryManager(Game game) : base()
        {
            _instance = this;
        }

        /// <summary>
        /// 发号：为新的 RoundMemory 分配唯一 ID
        /// </summary>
        public static long GetNewRoundMemoryId()
        {
            if (Instance == null)
            {
                Log.Error("[RoundMemory] 警告：发号时发现RoundMemory中控台不存在，返回-1");
                return -1;
            }
            return System.Threading.Interlocked.Increment(ref Instance._nextRoundMemoryId);
        }

        /// <summary>
        /// ⭐ v4.0.1: 简化版 - 只负责分发 RoundMemory 给各个 Pawn
        /// 不再维护全局列表，RoundMemory 只存储在 FourLayerMemoryComp.ABM 中
        /// </summary>
        public static void AddRoundMemory(RoundMemory roundMemory)
        {
            if (Instance == null)
            {
                Log.Error("[RoundMemory] 警告：成功捕获对话，但尝试添加对象时发现RoundMemory中控台不存在，无法添加");
                return;
            }

            // 空值/无效检查
            if (roundMemory == null || string.IsNullOrWhiteSpace(roundMemory.content) || roundMemory.RoundMemoryUniqueID == -1)
            {
                Log.Warning("[RoundMemory] Attempted to add Invalid RoundMemory.");
                return;
            }

            // ⭐ v4.0.1: 只分发给各个 Pawn，不再保存到全局列表
            var pawns = roundMemory.Pawns;
            if (pawns == null) return;
            foreach (var pawn in pawns)
            {
                if (pawn == null) continue;
                pawn.TryGetComp<FourLayerMemoryComp>()?.ActiveMemories?.Add(roundMemory);
            }
        }

        /// <summary>
        /// 重置去重缓存（每 120 ticks 自动过期）
        /// </summary>
        public static void AutoReset()
        {
            if (Instance == null) return;
            int currentTick = Find.TickManager?.TicksGame ?? -1;
            if (currentTick - Instance.LastContextTick > CONTEXT_EXPIRE_TICKS)
            {
                Instance.RoundMemoryCache.Clear();
                if (Prefs.DevMode) Log.Message($"[RoundMemory] 重置查重缓存");
            }
            Instance.LastContextTick = currentTick;
        }

        /// <summary>
        /// 注入 Pawn 的 ABM 记忆（支持跨 Pawn 去重）
        /// </summary>
        public static string InjectABM(Pawn pawn)
        {
            if (Instance == null) return string.Empty;
            var abmList = pawn?.TryGetComp<FourLayerMemoryComp>()?.ActiveMemories;
            if (abmList == null || abmList.Count == 0) return string.Empty;

            AutoReset();
            int maxRounds = RimTalkMemoryPatchMod.Settings?.maxABMInjectionRounds ?? 3;
            var stringList = new List<string>();
            int stackedLength = 0;
            int stackedCount = 0;

            // 按timestamp降序排序，与UI面板保持一致
            var sortedList = abmList.OrderByDescending(m => m.timestamp).ToList();

            foreach (var entry in sortedList)
            {
                if (stackedCount >= maxRounds || stackedLength > MaxInjectedLength) break;

                if (entry is not RoundMemory roundMemory)
                {
                    // 不是RoundMemory，直接添加
                    stackedCount++;
                    stringList.Add($"{stackedCount}. [{DynamicMemoryInjection.GetMemoryTypeTag(entry.type)}] {entry?.content} ({entry?.TimeAgoString})");
                    continue;
                }

                if (DevSwitch) continue;

                // 跨 Pawn 去重
                var roundMemoryCache = Instance.RoundMemoryCache;
                if (roundMemoryCache.Contains(roundMemory))
                {
                    if (Prefs.DevMode) Log.Message("[RoundMemory] 检测到重复RoundMemory，跳过注入");
                    continue;
                }
                roundMemoryCache.Add(roundMemory);

                var textBlock = roundMemory.content;
                if (textBlock == null)
                {
                    Log.Warning("[RoundMemory] 检测到RoundMemory文本丢失");
                    continue;
                }

                if (textBlock.Length > MaxTextBlockInjectedLength)
                {
                    textBlock = textBlock.Substring(0, MaxTextBlockInjectedLength) + "...";
                }

                stackedLength += textBlock.Length;
                stackedCount++;
                stringList.Add($"{stackedCount}. [{DynamicMemoryInjection.GetMemoryTypeTag(roundMemory.type)}]{textBlock}({roundMemory.TimeAgoString})");
            }

            return string.Join("\n", stringList);
        }

        // ⭐ v4.0.1: 简化存档 - 只保存发号机状态
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _nextRoundMemoryId, "NextRoundMemoryId", 0);
            
            // ⭐ 兼容旧存档：读取但忽略旧的 _roundMemories
            // 旧存档中的 RoundMemory 已经在各 Pawn 的 ABM 中，不需要再维护全局列表
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                List<RoundMemory> legacyRoundMemories = null;
                Scribe_Collections.Look(ref legacyRoundMemories, "RoundMemories", LookMode.Deep);
                if (legacyRoundMemories != null && legacyRoundMemories.Count > 0)
                {
                    Log.Message($"[RoundMemory] 检测到旧存档格式，忽略 {legacyRoundMemories.Count} 条全局 RoundMemory（已在各 Pawn ABM 中）");
                }
            }
        }

        // ⭐ v4.0.1: 已删除 FinalizeInit 指针修正
        // 原因：不再维护全局 _roundMemories 列表，RoundMemory 只存在于各 Pawn 的 ABM 中
        // 每个 Pawn 独立序列化自己的 ABM，不需要跨 Pawn 共享引用
    }
}
