using RimTalk.Memory.Utils;
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
                if (Current.Game is null)
                {
                    _instance = null; // 存档外时强制置空，避免误用
                    Log.Error("[RoundMemory] 尝试在存档外访问RoundMemory中控台");
                }
                return _instance; // 存档外时会返回null
            }
        }

        // 配置常量
        private const int MaxRoundMemory = 256; // 最大保存轮次记忆条目数
        public const int MaxContentLength = 16384; // 创建时单条RoundMemory最大文本长度

        // 核心: 轮次记忆环形缓冲区（按时间升序，最旧在前）
        private RimRingBuffer<RoundMemory> _roundMemories = new(MaxRoundMemory);
        public RimRingBuffer<RoundMemory> RoundMemories => _roundMemories;

        // 存读档用的临时list
        private List<RoundMemory> _tmpRoundMemories;

        // 发号机
        private long _nextRoundMemoryId = 0;

        // 玩家对话相关缓存
        private Pawn _playerPawn; // 调用时为空是正常的，需要在调用端进行空值检查
        private string _playerDialogue = string.Empty;

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

            // 直接向环形缓冲区添加，RimRingBuffer 内部会自动高效处理超出容量时的覆盖逻辑
            Instance._roundMemories.Add(roundMemory);

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

            Scribe_Values.Look(
                ref _nextRoundMemoryId,
                "NextRoundMemoryId",
                0
            );

            LookRoundMemories();

            Log.Message($"[RoundMemory] ExposeData for RoundMemory: count={_roundMemories.Count}; NextRoundMemoryId: {_nextRoundMemoryId}");
        }

        // 读写_roundMemories
        private void LookRoundMemories()
        {
            // 存档时，按从老到新的顺序提取出来，放到临时列表里交给 Scribe_Collections 来处理
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                _tmpRoundMemories = new();
                for (int i = 0; i < _roundMemories.Count; i++)
                {
                    _tmpRoundMemories.Add(_roundMemories[i]);
                }
            }

            // look look
            Scribe_Collections.Look(
                ref _tmpRoundMemories,
                "RoundMemories",
                LookMode.Deep
            );

            // 读档的最后一个阶段，tmpRoundMemories加载完成，把数据转移到真正的环形缓冲区里
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (_tmpRoundMemories is null) return; // 注意这里直接跳出了方法，如果后续有更多数据=null，可以把这里改成if
                // 把旧数据按顺序灌入已经基于当前编译尺寸初始化的缓冲区
                foreach (var roundMemory in _tmpRoundMemories)
                {
                    _roundMemories.Add(roundMemory);
                }
            }

            // 释放临时列表
            if (Scribe.mode == LoadSaveMode.Saving || Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                _tmpRoundMemories = null;
            }
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
            for (int i = 0; i < _roundMemories.Count; i++)
            {
                var roundMemory = _roundMemories[i];
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
