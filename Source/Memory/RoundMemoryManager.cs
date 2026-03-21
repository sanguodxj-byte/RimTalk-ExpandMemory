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
    /// 以下内容不能被移除：
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

        // 核心: 历史记录列表（按时间升序，最旧在前）
        private List<RoundMemory> _roundMemories = new();
        // 使用属性封装，确保不为 null，但不确定有没有必要
        public List<RoundMemory> RoundMemories
        {
            get
            {
                if (_roundMemories == null)
                {
                    _roundMemories = new();
                    Log.Warning("[RoundMemory] 检测到 _roundMemories 为空，创建新列表");
                }
                return _roundMemories;
            }
            set => _roundMemories = value;
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
        public static int MaxRoundMemory = 500; // 最大保存轮次记忆条目数
        public static int MaxTextBlockLength = 10000; // 创建时单条RoundMemory最大文本长度
        public static int MaxTextBlockInjectedLength = 5000; // 注入时单条RoundMemory最大文本长度
        public static int MaxInjectedLength = 20000; // 注入时最大总文本长度

        public static bool IsPlayerDialogueInject => RimTalkMemoryPatchMod.Settings?.IsPlayerDialogueInject ?? true;

        public RoundMemoryManager(Game game) : base()
        {
            _instance = this; // 初始化时将自己赋值给静态实例
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
        /// 安全添加轮次记忆
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

            // 当达到或超过上限时，移除最旧的条目，直到有空间，然后添加新条目
            var roundMemories = Instance.RoundMemories;
            while (roundMemories.Count >= MaxRoundMemory)
            {
                roundMemories.RemoveAt(0);
            }

            roundMemories.Add(roundMemory);

            // 分配历史给各个Pawn
            var pawns = roundMemory.Pawns;
            if (pawns == null) return;
            foreach (var pawn in pawns)
            {
                if (pawn == null) continue;
                pawn.TryGetComp<FourLayerMemoryComp>()?.ActiveMemories?.Add(roundMemory); // 直接访问属性并添加的写法不是很好，有待优化
            }
        }

        /// <summary>
        /// 重置去重缓存
        /// </summary>
        public static void ResetDuplicateCache()
        {
            Instance?.RoundMemoryCache.Clear();
            if (Prefs.DevMode) Log.Message($"[RoundMemory] 重置查重缓存");
        }

        // ⭐ v5.0: InjectABM 方法已移动到 RimTalk.Memory.Injection.ABMCollector
        // 旧代码已删除，去重逻辑通过 AutoReset() 和 RoundMemoryCache 继续提供
        // ABMCollector 直接调用这些方法/属性

        // ⭐ v4.0.1: 简化存档 - 只保存发号机状态
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(
                ref _roundMemories,
                "RoundMemories",
                LookMode.Deep
            );
            Scribe_Values.Look(
                ref _nextRoundMemoryId,
                "NextRoundMemoryId",
                0
            );

            // 确保加载后不为 null
            if (_roundMemories == null)
            {
                _roundMemories = new();
                Log.Warning($"[RoundMemory] 未找到已有 _roundMemories，新建空列表");
            }

            Log.Message($"[RoundMemory] ExposeData for RoundMemory: count={RoundMemories.Count}");
        }

        // 在读档后修正各 Pawn 上的指针
        public override void FinalizeInit()
        {
            // 1. 建立去重索引
            Dictionary<long, RoundMemory> managerMap = new Dictionary<long, RoundMemory>();
            if (_roundMemories == null)
            {
                Log.Warning("[RoundMemory] RoundMemory为空，无法进行指针修正");
                return;
            }
            foreach (var roundMemory in _roundMemories)
            {
                if (roundMemory == null)
                {
                    Log.Warning("[RoundMemory] 检测到RoundMemory中有 null 条目，跳过");
                    continue;
                }
                var roundMemoryUniqueID = roundMemory.RoundMemoryUniqueID;
                if (!managerMap.ContainsKey(roundMemoryUniqueID))
                {
                    managerMap.Add(roundMemoryUniqueID, roundMemory);
                }
            }

            // 2. 获取Pawn
            var allPawns = PawnsFinder.All_AliveOrDead;

            // 3. 开始去重(O(N))
            foreach (var pawn in allPawns)
            {
                // --- 剪枝 1：TryGetComp ---
                var comp = pawn.TryGetComp<FourLayerMemoryComp>();
                if (comp == null) continue;

                // --- 剪枝 2：列表为空 ---
                var ABMs = comp.ActiveMemories;
                if (ABMs == null || ABMs.Count == 0) continue;
                // --- 只有少部分有数据的 Pawn 会进入这里 ---
                // 4. 指针替换
                for (int i = 0; i < ABMs.Count; i++)
                {
                    var ABM = ABMs[i];

                    // 如果是 null 或者不为 History 或者 ID 不在主表里或者和manager指向的就是同一个（一般不可能），不管它
                    if (ABM == null || ABM is not RoundMemory ABMRef || !managerMap.TryGetValue(ABMRef.RoundMemoryUniqueID, out RoundMemory managerRef) || ABMRef == managerRef) continue;
                    // ★ 核心动作：狸猫换太子
                    // 替换后，localRef 变成垃圾，等待 GC 回收
                    ABMs[i] = managerRef;
                    if (Prefs.DevMode) Log.Message("[RoundMemory] ABM指针已修正");
                }
            }
        }
    }
}
