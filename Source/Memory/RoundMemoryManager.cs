using RimTalk.Data;
using RimTalk.Memory;
using RimTalk.MemoryPatch;
using RimTalk.Prompt;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.Noise;

namespace RimTalk.Memory
{
    // 历史中控
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
                    Log.Warning("[轮次记忆] 轮次记忆中控台仅在存档内有效");
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
                    Log.Warning("[轮次记忆] 检测到 _roundMemories 为空，创建新列表");
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
        // 查重缓存
        public int LastContextTick = -1;
        private const int CONTEXT_EXPIRE_TICKS = 120;
        private HashSet<RoundMemory> _roundMemoryCache;
        public HashSet<RoundMemory> RoundMemoryCache
        {
            get
            {
                if (_roundMemoryCache == null)
                {
                    _roundMemoryCache = new(); // 仅在使用时初始化
                }
                return _roundMemoryCache;
            }
            set => _roundMemoryCache = value;
        }

        // 发号机
        public long _nextRoundMemoryId = 0;

        public static bool DevSwitch = false;
        public static int MaxRoundMemory = 500; // 最大保存轮次记忆条目数，不对用户开放，留作防御性编程
        public static int MaxTextBlockLength = 10000; // 保存时单条轮次记忆最大文本长度
        public static int MaxRoundMemoryInjected = 10; // 最大注入轮次记忆条目数
        public static int MaxTextBlockInjectedLength = 5000; // 注入时单条轮次记忆最大文本长度
        public static int MaxInjectedLength = 20000; // 注入时最大总文本长度

        public static bool IsPlayerDialogueInject => RimTalkMemoryPatchMod.Settings?.IsPlayerDialogueInject ?? true; // 是否注入玩家发言

        public RoundMemoryManager(Game game) : base()
        {
            _instance = this; // 初始化时将自己赋值给静态实例
            // Log.Message("[RimTalkHistoryPlus] HistoryManager initialized.");
        }

        // 发号
        public static long GetNewRoundMemoryId()
        {
            if (Instance == null)
            {
                Log.Error("[轮次记忆] 警告：发号时发现轮次记忆中控台不存在，返回-1");
                return -1;
            }

            // 这一行代码做了两件事：
            // a. 把 _nextId 加 1
            // b. 返回加完之后的值
            return System.Threading.Interlocked.Increment(ref Instance._nextRoundMemoryId);
        }

        /// <summary>
        /// 安全添加历史记录到管理器，会自动保证不超过最大长度。
        /// </summary>
        public static void AddRoundMemory(RoundMemory roundMemory)
        {
            if (Instance == null)
            {
                Log.Error("[轮次记忆] 警告：成功捕获对话，但尝试添加对象时发现轮次记忆中控台不存在，无法添加");
                return;
            }

            // 空值/无效检查
            if (roundMemory == null || string.IsNullOrWhiteSpace(roundMemory.content) || roundMemory.RoundMemoryUniqueID == -1)
            {
                Log.Warning("[轮次记忆] Attempted to add Invalid 轮次记忆.");
                return;
            }

            // 当达到或超过上限时，移除最旧的条目，直到有空间，然后添加新条目
            var roundMemories = Instance.RoundMemories;
            while (roundMemories.Count >= MaxRoundMemory)
            {
                roundMemories.RemoveAt(0);
                // Log.Message("[RimTalkHistoryPlus] Removed oldest history to maintain maxHistory limit.");
            }

            roundMemories.Add(roundMemory);
            // Log.Message($"[RimTalkHistoryPlus] Added History. Total count: {histories.Count}");

            // 分配历史给各个Pawn
            var pawns = roundMemory.Pawns;
            if (pawns == null) return;
            foreach (var pawn in pawns)
            {
                if (pawn == null) continue;
                pawn.TryGetComp<FourLayerMemoryComp>()?.ActiveMemories?.Add(roundMemory); // 直接访问属性并添加的写法不是很好，有待优化
                // Log.Message($"[RimTalkHistoryPlus] 添加历史记忆给{pawn.Name}");
            }
        }

        /// <summary>
        /// 注入符合条件的历史。
        /// </summary>
        public static string InjectRoundMemory(PromptContext promptContext)
        {
            if (Instance == null) return string.Empty;

            var roundMemories = Instance.RoundMemories;

            if (roundMemories.Count == 0)
            {
                Log.Warning("[轮次记忆] No 轮次记忆 to inject.");
                return string.Empty;
            }
            // 用栈来反转顺序，确保注入时是从旧到新
            var stack = new Stack<string>();
            int stackedLength = 0;
            if (promptContext?.AllPawns != null)
            {
                for (int i = roundMemories.Count - 1; i >= 0; i--)
                {
                    var roundMemory = roundMemories[i];
                    // 只注入与当前对话参与者有交集的历史
                    // 这里做了null检查，顺便剔除null，但或许可以在读档时就剔除？
                    // 哈基米评价这个if为“利用了编译器特性实现的奇技淫巧”，阅读的时候请仔细注意
                    if (roundMemory?.Pawns?.RemoveWhere(p => p == null) == null || !roundMemory.Pawns.Overlaps(promptContext.AllPawns)) continue;
                    if (roundMemory.content == null)
                    {
                        Log.Warning("[轮次记忆] 检测到轮次记忆文本丢失");
                        continue;
                    }
                    // 限制单条注入历史长度
                    var textBlock = roundMemory.content;
                    if (textBlock.Length > MaxTextBlockInjectedLength)
                    {
                        textBlock = textBlock.Substring(0, MaxTextBlockInjectedLength) + "...";
                    }
                    // 你过关！入栈！
                    stack.Push($"{roundMemory.GetDateAndTime()}\n{textBlock}");
                    stackedLength += textBlock.Length;
                    // 达到注入上限，停止
                    if (stack.Count >= MaxRoundMemoryInjected || stackedLength >= MaxInjectedLength) break;
                }
            }
            else
            {
                // promptContext为空或其中没有AllPawns，可能是出错了，注入最近历史
                Log.Warning($"[轮次记忆] InjectRoundMemory called with null PromptContext or AllPawns. 注入最晚{MaxRoundMemoryInjected}条记录");
                for (int i = roundMemories.Count - 1; i >= 0; i--)
                {
                    var roundMemory = roundMemories[i];
                    // 限制单条注入历史长度
                    var textBlock = roundMemory.content;
                    if (textBlock.Length > MaxTextBlockInjectedLength)
                    {
                        textBlock = textBlock.Substring(0, MaxTextBlockInjectedLength) + "...";
                    }
                    // 直接入栈
                    stack.Push($"{roundMemory.GetDateAndTime()}\n{roundMemory.content}");
                    stackedLength += textBlock.Length;
                    // 达到注入上限，停止
                    if (stack.Count >= MaxRoundMemoryInjected || stackedLength >= MaxInjectedLength) break;
                }
            }

            // Log.Message($"[RimTalkHistoryPlus] 注入{stack.Count}条历史");
            return string.Join("\n\n", stack);
        }

        // 方便开发，暂时先放在这里，反正都是静态成员，随时可以搬
        public static void AutoReset()
        {
            int currentTick = Find.TickManager?.TicksGame ?? -1;
            // 如果距离上次调用超过阈值，重置查重缓存
            if (currentTick - Instance.LastContextTick > CONTEXT_EXPIRE_TICKS)
            {
                Instance.RoundMemoryCache.Clear();
                Log.Message($"[轮次记忆] 重置查重缓存");
            }
            Instance.LastContextTick = currentTick;
        }
        public static string InjectABM(Pawn pawn)
        {
            // 无效值判断
            if (Instance == null) return string.Empty;
            var abmList = pawn?.TryGetComp<FourLayerMemoryComp>()?.ActiveMemories;
            if (abmList == null || abmList.Count == 0) return string.Empty;

            // 这里不需要反转顺序
            // var stack = new Stack<string>();
            AutoReset();
            int maxRounds = RimTalkMemoryPatchMod.Settings?.maxABMInjectionRounds ?? 3;
            var stringList = new List<string>();
            int stackedLength = 0;
            int stackedCount = 0;
            for (int i = abmList.Count - 1; i >= 0; i--)
            {
                // 达到注入上限，停止
                if (stackedCount > maxRounds || stackedLength > MaxInjectedLength) break;
                if (abmList[i] is not RoundMemory roundMemory)
                {
                    // 不是轮次记忆，直接过关
                    stackedCount++;
                    var abm = abmList[i];
                    stringList.Add($"{stackedCount}. [{DynamicMemoryInjection.GetMemoryTypeTag(abm.type)}] {abm?.content} ({abm?.TimeAgoString})");
                    continue;
                }
                if (DevSwitch) continue;
                // 直接在对象层面查重
                var roundMemoryCache = Instance.RoundMemoryCache;
                if (roundMemoryCache.Contains(roundMemory))
                {
                    Log.Message("[轮次记忆] 检测到重复轮次记忆，跳过注入");
                    continue;
                }
                roundMemoryCache.Add(roundMemory);
                // 开始构建文本
                var textBlock = roundMemory.content;
                if (textBlock == null)
                {
                    Log.Warning("[轮次记忆] 检测到轮次记忆文本丢失");
                    continue;
                }
                // 限制单条注入轮次记忆长度
                if (textBlock.Length > MaxTextBlockInjectedLength)
                {
                    textBlock = textBlock.Substring(0, MaxTextBlockInjectedLength) + "...";
                }
                // 你过关！
                stackedLength += textBlock.Length;
                stackedCount++;
                stringList.Add($"{stackedCount}. [{DynamicMemoryInjection.GetMemoryTypeTag(roundMemory.type)}]{textBlock}({roundMemory.TimeAgoString})");
            }

            return string.Join("\n", stringList);
        }

        // 存读档
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
                Log.Warning($"[轮次记忆] 未找到已有 _roundMemories，新建空列表");
            }

            Log.Message($"[轮次记忆] ExposeData for 轮次记忆: count={RoundMemories.Count}");
        }

        // 感谢四层记忆组件的屎山，我们需要在读档后修正各 Pawn 上的指针
        public override void FinalizeInit()
        {
            // 1. 【索引构建】建立“真身”索引 (O(M))
            // 去掉 null，建立 ID -> Object 映射，方便 O(1) 查找
            Dictionary<long, RoundMemory> managerMap = new Dictionary<long, RoundMemory>();
            if (_roundMemories == null)
            {
                Log.Warning("[轮次记忆] 轮次记忆为空，无法进行指针修正");
                return;
            }
            foreach (var roundMemory in _roundMemories)
            {
                if (roundMemory == null)
                {
                    Log.Warning("[轮次记忆] 检测到轮次记忆中有 null 条目，跳过");
                    continue;
                }
                var roundMemoryUniqueID = roundMemory.RoundMemoryUniqueID;
                if (!managerMap.ContainsKey(roundMemoryUniqueID))
                {
                    managerMap.Add(roundMemoryUniqueID, roundMemory);
                }
            }

            // 2. 【获取全员】拿到游戏里所有的小人 (包括死人、世界上的、地图上的)
            // 这一步可能会产生一个较大的临时 List，但在 Loading 阶段是安全的
            var allPawns = PawnsFinder.All_AliveOrDead;

            // 3. 【遍历清洗】(O(N))
            foreach (var pawn in allPawns)
            {
                // --- 剪枝 1：没组件 ---
                // TryGetComp 非常快
                // 不确定这一步空判断有没有意义，一个Pawn可能没有组件吗
                var comp = pawn.TryGetComp<FourLayerMemoryComp>();
                if (comp == null) continue;

                // --- 剪枝 2：列表为空 ---
                // 大部分 Pawn 会在这里直接跳过，耗时极低
                var SCMs = comp.SituationalMemories;
                if (SCMs != null && SCMs.Count > 0)
                {

                    // --- 只有少部分有数据的 Pawn 会进入这里 ---
                    // 4. 【指针替换】
                    for (int i = 0; i < SCMs.Count; i++)
                    {
                        var SCM = SCMs[i];

                        // 如果是 null 或者不为 History 或者 ID 不在主表里或者和manager指向的就是同一个（一般不可能），不管它
                        if (SCM == null || SCM is not RoundMemory SCMRef || !managerMap.TryGetValue(SCMRef.RoundMemoryUniqueID, out RoundMemory managerRef) || SCMRef == managerRef) continue;
                        // ★ 核心动作：狸猫换太子
                        // 替换后，localRef 变成垃圾，等待 GC 回收
                        SCMs[i] = managerRef;
                        if(Prefs.DevMode) Log.Message("[轮次记忆] SCM指针已修正");
                    }
                }

                // 补充ABM修正
                var ABMs = comp.ActiveMemories;
                if (ABMs != null && ABMs.Count > 0)
                {

                    // --- 只有少部分有数据的 Pawn 会进入这里 ---
                    // 4. 【指针替换】
                    for (int i = 0; i < ABMs.Count; i++)
                    {
                        var ABM = ABMs[i];

                        // 如果是 null 或者不为 History 或者 ID 不在主表里或者和manager指向的就是同一个（一般不可能），不管它
                        if (ABM == null || ABM is not RoundMemory ABMRef || !managerMap.TryGetValue(ABMRef.RoundMemoryUniqueID, out RoundMemory managerRef) || ABMRef == managerRef) continue;
                        // ★ 核心动作：狸猫换太子
                        // 替换后，localRef 变成垃圾，等待 GC 回收
                        ABMs[i] = managerRef;
                        if (Prefs.DevMode) Log.Message("[轮次记忆] ABM指针已修正");
                    }
                }
            }
        }

    }
}
