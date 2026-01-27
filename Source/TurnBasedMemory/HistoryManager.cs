using RimTalk.Data;
using RimTalk.Memory;
using RimTalk.MemoryPatch;
using RimTalk.Prompt;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace RimTalkHistoryPlus
{
    // 历史中控
    public class HistoryManager : GameComponent
    {
        // Manager实例，供静态方法访问，仅在存档内有效
        private static HistoryManager _instance;
        public static HistoryManager Instance
        {
            get
            {
                if (Current.Game == null)
                {
                    Log.Warning("[RimTalkHistoryPlus] HistoryManager 仅在存档内有效");
                    return null;
                }
                return _instance;
            }
        }
        // 核心: 历史记录列表（按时间升序，最旧在前）
        private List<History> _histories = new();
        // 使用属性封装，确保不为 null，但不确定有没有必要
        private List<History> Histories
        {
            get
            {
                if (_histories == null)
                {
                    _histories = new();
                    Log.Warning("[RimTalkHistoryPlus] 检测到 _histories 为空，创建新列表");
                }
                return _histories;
            }
            set => _histories = value;
        }
        // 玩家对话缓存
        private string _playerDialogue = string.Empty;
        public string PlayerDialogue
        {
            get => _playerDialogue;
            set => _playerDialogue = value;
        }
        // 查重缓存
        private int _lastContextTick = -1;
        private const int CONTEXT_EXPIRE_TICKS = 120;
        private HashSet<History> _historyCache;
        private HashSet<History> HistoryCache
        {
            get
            {
                if (_historyCache == null)
                {
                    _historyCache = new(); // 仅在使用时初始化
                }
                return _historyCache;
            }
            set => _historyCache = value;
        }

        // 发号机
        private long _nextHistoryId = 0;


        public static int MaxHistory = 500; // 最大保存历史条目数，不对用户开放，留作防御性编程
        public static int MaxTextBlockLength = 10000; // 保存时单条轮次记忆最大文本长度
        public static int MaxHistoryInjected => RimTalkMemoryPatchMod.Settings?.maxABMInjectionRounds ?? 4; // 最大注入轮次记忆条目数
        public static int MaxTextBlockInjectedLength = 5000; // 注入时单条轮次记忆最大文本长度
        public static int MaxInjectedLength = 20000; // 注入时最大总文本长度

        public HistoryManager(Game game) : base()
        {
            _instance = this; // 初始化时将自己赋值给静态实例
            // Log.Message("[RimTalkHistoryPlus] HistoryManager initialized.");
        }

        // 发号
        public static long GetNewHistoryId()
        {
            if (Instance == null)
            {
                Log.Error("[RimTalkHistoryPlus] 警告：发号时发现历史中控台不存在，返回-1");
                return -1;
            }

            // 这一行代码做了两件事：
            // a. 把 _nextId 加 1
            // b. 返回加完之后的值
            return System.Threading.Interlocked.Increment(ref Instance._nextHistoryId);
        }

        /// <summary>
        /// 安全添加历史记录到管理器，会自动保证不超过最大长度。
        /// </summary>
        public static void AddHistory(History history)
        {
            if (Instance == null)
            {
                Log.Error("[RimTalkHistoryPlus] 警告：成功捕获对话，但尝试添加对象时发现历史中控台不存在，无法添加");
                return;
            }

            // 空值/无效检查
            if (history == null || string.IsNullOrWhiteSpace(history.content) || history.HistoryUniqueID == -1)
            {
                Log.Warning("[RimTalkHistoryPlus] Attempted to add Invalid History.");
                return;
            }

            // 当达到或超过上限时，移除最旧的条目，直到有空间，然后添加新条目
            var histories = Instance.Histories;
            while (histories.Count >= MaxHistory)
            {
                histories.RemoveAt(0);
                // Log.Message("[RimTalkHistoryPlus] Removed oldest history to maintain maxHistory limit.");
            }

            histories.Add(history);
            // Log.Message($"[RimTalkHistoryPlus] Added History. Total count: {histories.Count}");

            // 分配历史给各个Pawn
            var pawns = history.Pawns;
            if (pawns == null) return;
            foreach (var pawn in pawns)
            {
                if (pawn == null) continue;
                pawn.TryGetComp<FourLayerMemoryComp>()?.ActiveMemories?.Add(history); // 直接访问属性并添加的写法不是很好，有待优化
                // Log.Message($"[RimTalkHistoryPlus] 添加历史记忆给{pawn.Name}");
            }
        }

        /// <summary>
        /// 注入符合条件的历史。
        /// </summary>
        public static string InjectHistory(PromptContext promptContext)
        {
            if (Instance == null) return string.Empty;

            var histories = Instance.Histories;

            if (histories.Count == 0)
            {
                Log.Warning("[RimTalkHistoryPlus] No histories to inject.");
                return string.Empty;
            }
            // 用栈来反转顺序，确保注入时是从旧到新
            var stack = new Stack<string>();
            int stackedLength = 0;
            if (promptContext?.AllPawns != null)
            {
                for (int i = histories.Count - 1; i >= 0; i--)
                {
                    var history = histories[i];
                    // 只注入与当前对话参与者有交集的历史
                    // 这里做了null检查，顺便剔除null，但或许可以在读档时就剔除？
                    // 哈基米评价这个if为“利用了编译器特性实现的奇技淫巧”，阅读的时候请仔细注意
                    if (history?.Pawns?.RemoveWhere(p => p == null) == null || !history.Pawns.Overlaps(promptContext.AllPawns)) continue;
                    if (history.content == null)
                    {
                        Log.Warning("[RimTalkHistoryPlus] 检测到历史文本丢失");
                        continue;
                    }
                    // 限制单条注入历史长度
                    var textBlock = history.content;
                    if (textBlock.Length > MaxTextBlockInjectedLength)
                    {
                        textBlock = textBlock.Substring(0, MaxTextBlockInjectedLength) + "...";
                    }
                    // 你过关！入栈！
                    stack.Push($"{history.GetDateAndTime()}\n{textBlock}");
                    stackedLength += textBlock.Length;
                    // 达到注入上限，停止
                    if (stack.Count >= MaxHistoryInjected || stackedLength >= MaxInjectedLength) break;
                }
            }
            else
            {
                // promptContext为空或其中没有AllPawns，可能是出错了，注入最近历史
                Log.Warning($"[RimTalkHistoryPlus] InjectHistory called with null PromptContext or AllPawns. 注入最晚{MaxHistoryInjected}条记录");
                for (int i = histories.Count - 1; i >= 0; i--)
                {
                    var history = histories[i];
                    // 限制单条注入历史长度
                    var textBlock = history.content;
                    if (textBlock.Length > MaxTextBlockInjectedLength)
                    {
                        textBlock = textBlock.Substring(0, MaxTextBlockInjectedLength) + "...";
                    }
                    // 直接入栈
                    stack.Push($"{history.GetDateAndTime()}\n{history.content}");
                    stackedLength += textBlock.Length;
                    // 达到注入上限，停止
                    if (stack.Count >= MaxHistoryInjected || stackedLength >= MaxInjectedLength) break;
                }
            }

            // Log.Message($"[RimTalkHistoryPlus] 注入{stack.Count}条历史");
            return string.Join("\n\n", stack);
        }

        // 方便开发，暂时先放在这里，反正都是静态成员，随时可以搬
        private static void AutoReset()
        {
            int currentTick = Find.TickManager?.TicksGame ?? -1;
            // 如果距离上次调用超过阈值，重置查重缓存
            if (currentTick - Instance._lastContextTick > CONTEXT_EXPIRE_TICKS)
            {
                Instance.HistoryCache.Clear();
                Log.Message($"[RimTalkHistoryPlus] 重置查重缓存");
            }
            Instance._lastContextTick = currentTick;
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
            int maxRounds = MaxHistoryInjected;
            var stringList = new List<string>();
            int stackedLength = 0;
            int stackedCount = 0;
            for (int i = abmList.Count - 1; i >= 0; i--)
            {
                if (abmList[i] is not History history)
                {
                    // 不是轮次记忆，直接过关
                    stringList.Add(abmList[i]?.content ?? string.Empty);
                    continue;
                }
                // 达到注入上限，停止
                if (stackedCount > maxRounds || stackedLength > MaxInjectedLength) break;
                // 直接在对象层面查重
                var historyCache = Instance.HistoryCache;
                if (historyCache.Contains(history))
                {
                    Log.Message("[RimTalkHistoryPlus] 检测到重复历史，跳过注入");
                    continue;
                }
                historyCache.Add(history);
                // 开始构建文本
                var textBlock = history.content;
                if (textBlock == null)
                {
                    Log.Warning("[RimTalkHistoryPlus] 检测到历史文本丢失");
                    continue;
                }
                // 限制单条注入历史长度
                if (textBlock.Length > MaxTextBlockInjectedLength)
                {
                    textBlock = textBlock.Substring(0, MaxTextBlockInjectedLength) + "...";
                }
                // 你过关！
                stringList.Add($"[{history.GetParticipants()}的对话]\n{textBlock}");
                stackedLength += textBlock.Length;
                stackedCount++;
            }

            return string.Join("\n\n", stringList);
        }

        // 存读档
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(
                ref _histories,
                "Histories",
                LookMode.Deep
            );
            Scribe_Values.Look(
                ref _nextHistoryId,
                "NextHistoryId",
                0
            );

            // 确保加载后不为 null
            if (_histories == null)
            {
                _histories = new();
                Log.Warning($"[RimTalkHistoryPlus] 未找到已有 _histories，新建空列表");
            }

            Log.Message($"[RimTalkHistoryPlus] ExposeData for Histories: count={Histories.Count}");
        }

        // 感谢四层记忆组件的屎山，我们需要在读档后修正各 Pawn 上的指针
        public override void FinalizeInit()
        {
            // 1. 【索引构建】建立“真身”索引 (O(M))
            // 去掉 null，建立 ID -> Object 映射，方便 O(1) 查找
            Dictionary<long, History> managerMap = new Dictionary<long, History>();
            if (_histories == null)
            {
                Log.Warning("[RimTalkHistoryPlus] _histories 为空，无法进行指针修正");
                return;
            }
            foreach (var history in _histories)
            {
                if (history == null)
                {
                    Log.Warning("[RimTalkHistoryPlus] 检测到 _histories 中有 null 条目，跳过");
                    continue;
                }
                var historyUniqueID = history.HistoryUniqueID;
                if (!managerMap.ContainsKey(historyUniqueID))
                {
                    managerMap.Add(historyUniqueID, history);
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
                        if (SCM == null || SCM is not History SCMRef || !managerMap.TryGetValue(SCMRef.HistoryUniqueID, out History managerRef) || SCMRef == managerRef) continue;
                        // ★ 核心动作：狸猫换太子
                        // 替换后，localRef 变成垃圾，等待 GC 回收
                        SCMs[i] = managerRef;
                        Log.Message("[RimTalkHistoryPlus] SCM指针已修正");
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
                        if (ABM == null || ABM is not History ABMRef || !managerMap.TryGetValue(ABMRef.HistoryUniqueID, out History managerRef) || ABMRef == managerRef) continue;
                        // ★ 核心动作：狸猫换太子
                        // 替换后，localRef 变成垃圾，等待 GC 回收
                        ABMs[i] = managerRef;
                        Log.Message("[RimTalkHistoryPlus] ABM指针已修正");
                    }
                }
            }
        }

    }
}
