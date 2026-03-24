using RimTalk.MemoryPatch;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace RimTalk.Memory
{

    // 轮次记忆中控台
    public class RoundMemoryManager : GameComponent
    {
        // Manager实例，供静态方法访问，仅在存档内有效
        private static RoundMemoryManager _instance;
        // 只读使用
        public static RoundMemoryManager Instance
        {
            get
            {
                if (Current.Game is null) Log.Error("[RoundMemory] 尝试在存档外访问RoundMemory中控台");
                return _instance; // 存档外时会返回null
            }
        }

        // 核心: 轮次记忆列表（按时间升序，最旧在前）
        // List尚有优化空间
        // 环形缓冲区就很不错，我甚至都写完了
        // 但环世界的存档系统不好存这个，遂放弃
        private List<RoundMemory> _roundMemories = new();
        public List<RoundMemory> RoundMemories => _roundMemories;

        // 发号机
        private long _nextRoundMemoryId = 0;

        // 玩家对话相关缓存
        private Pawn _playerPawn; // 调用时为空是正常的，需要在调用端进行空值检查
        private string _playerDialogue = string.Empty;

        // 配置常量
        private const int MaxRoundMemory = 256; // 最大保存轮次记忆条目数
        public const int MaxContentLength = 16384; // 创建时单条RoundMemory最大文本长度

        public RoundMemoryManager(Game game) : base()
        {
            _instance = this; // 初始化时将自己赋值给静态实例
        }

        /// <summary>
        /// 发号：为新的 RoundMemory 分配唯一 ID
        /// </summary>
        public static long GetNewRoundMemoryId()
        {
            if (Instance is null)
            {
                Log.Error("[RoundMemory] 警告：发号时发现RoundMemory中控台不存在，返回-1");
                return -1;
            }
            return System.Threading.Interlocked.Increment(ref Instance._nextRoundMemoryId);
        }

        /// <summary>
        /// 捕获玩家对话
        /// </summary>
        public static void CapturePlayerDialogue(Pawn playerPawn, string playerDialogue)
        {
            if (Instance is null)
            {
                Log.Error("[RoundMemory] 警告：捕获玩家对话时发现RoundMemory中控台不存在");
                return;
            }
            Instance._playerPawn = playerPawn;

            string playerName = playerPawn?.LabelShort;
            Instance._playerDialogue = $"{(string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName)}: {playerDialogue}";

            Log.Message($"[RoundMemory] 成功捕获玩家发言");
        }

        /// <summary>
        /// 构建并添加轮次记忆
        /// </summary>
        public static void BuildRoundMemory(HashSet<Pawn> pawns, string content, bool isPlayerInitiate = false)
        {
            if (Instance is null)
            {
                Log.Error("[RoundMemory] 警告：构建轮次记忆时发现RoundMemory中控台不存在");
                return;
            }

            // 如果对话为玩家发起且启用相关配置项，则提前修饰数据
            if (isPlayerInitiate && (RimTalkMemoryPatchMod.Settings?.IsPlayerDialogueInject ?? true))
            {
                pawns?.Add(Instance._playerPawn);
                content = $"{Instance._playerDialogue}\n{content}";
                Log.Message("[RoundMemory] 成功插入玩家文本");
            }

            // 构建新的 RoundMemory 实例
            var roundMemory = new RoundMemory(pawns, content);

            // 当达到或超过上限时，移除最旧的条目，直到有空间，然后添加新条目
            var roundMemories = Instance._roundMemories;
            while (roundMemories.Count >= MaxRoundMemory)
            {
                roundMemories.RemoveAt(0);
            }

            roundMemories.Add(roundMemory);

            // 分配历史给各个Pawn
            if (pawns is null) return;
            foreach (var pawn in pawns)
            {
                if (pawn is null) continue;
                pawn.TryGetComp<FourLayerMemoryComp>()?.ActiveMemories?.Add(roundMemory); // 直接访问属性并添加的写法不是很好
            }
        }

        // 存档读写
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

            // 确保集合不为null
            if (_roundMemories is null)
            {
                _roundMemories = new();
                Log.Warning($"[RoundMemory] 未找到已有 roundMemories，新建空列表");
            }

            Log.Message($"[RoundMemory] ExposeData for RoundMemory: count={_roundMemories.Count}");
        }

        // 在读档后修正各 Pawn 上的指针
        public override void FinalizeInit()
        {
            // 1. 建立去重索引
            Dictionary<long, RoundMemory> managerMap = new();
            if (_roundMemories is null)
            {
                Log.Warning("[RoundMemory] RoundMemory为空，无法进行指针修正");
                return;
            }
            foreach (var roundMemory in _roundMemories)
            {
                if (roundMemory is null)
                {
                    Log.Warning("[RoundMemory] 检测到RoundMemory中有 null 条目，跳过");
                    continue;
                }
                managerMap[roundMemory.RoundMemoryUniqueID] = roundMemory;
            }

            // 2. 获取Pawn
            var allPawns = PawnsFinder.All_AliveOrDead;
            if (allPawns is null) return;

            // 3. 开始去重(O(N))
            foreach (var pawn in allPawns)
            {
                // --- 剪枝 1：TryGetComp ---
                var comp = pawn?.TryGetComp<FourLayerMemoryComp>();
                if (comp is null) continue;

                // --- 剪枝 2：列表为空 ---
                var ABMs = comp.ActiveMemories;
                if (ABMs is null || ABMs.Count == 0) continue;

                // --- 只有少部分有数据的 Pawn 会进入这里 ---
                // 4. 指针替换
                for (int i = 0; i < ABMs.Count; i++)
                {
                    var ABM = ABMs[i];

                    // 如果是 null 或者不为 RoundMemory 或者 ID 不在主表里或者和manager指向的就是同一个（一般不可能），不管它
                    if (ABM is null
                        || ABM is not RoundMemory ABMRef
                        || !managerMap.TryGetValue(ABMRef.RoundMemoryUniqueID, out var managerRef)
                        || ABMRef == managerRef)
                        continue;

                    // ★ 核心动作：狸猫换太子
                    // 替换后，localRef 变成垃圾，等待 GC 回收
                    ABMs[i] = managerRef;

                    if (Prefs.DevMode) Log.Message("[RoundMemory] ABM指针已修正");
                }
            }
        }
    }

}
