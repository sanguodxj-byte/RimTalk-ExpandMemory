using RimTalk.Memory.Capture;
using RimTalk.Memory.Maintenance;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace RimTalk.Memory;

/// <summary>
/// 四层记忆系统核心组件
/// ABM -> SCM -> ELS -> CLPA
/// </summary>
public class FourLayerMemoryComp : ThingComp, IExposable
{
    // 核心记忆存储
    private List<MemoryEntry> _activeMemories = new();
    private List<MemoryEntry> _situationalMemories = new();
    private List<MemoryEntry> _eventLogMemories = new();
    private List<MemoryEntry> _archiveMemories = new();
    private HashSet<long> _summarizedIds;

    // 业务子模块
    private readonly JobMemoryCapturer _jobCapturer;
    private readonly CombatMemoryCapturer _combatCapturer;
    private readonly MemoryMaintainer _maintainer;
    private readonly MemorySummarizer _summarizer;
    private readonly MemoryInteractor _interactor;

    // 对外属性访问
    /// <summary>
    /// ABM: 完整对话记录，无容量限制，总结后转 ELS
    /// </summary>
    public List<MemoryEntry> ActiveMemories => _activeMemories;

    /// <summary>
    /// SCM: 固定后的轮次记忆ABM，或是非轮次记忆的过渡层
    /// </summary>
    public List<MemoryEntry> SituationalMemories => _situationalMemories;

    /// <summary>
    /// ELS: 总结后的记忆
    /// </summary>
    public List<MemoryEntry> EventLogMemories => _eventLogMemories;

    /// <summary>
    /// CLPA: 归档后的记忆
    /// </summary>
    public List<MemoryEntry> ArchiveMemories => _archiveMemories;

    /// <summary>
    /// 已总结的记忆ID集合，避免重复总结，需长久随存档保存
    /// 懒加载，未激活时无任何开销
    /// </summary>
    public HashSet<long> SummarizedIds => _summarizedIds ??= new();

    /// <summary>
    /// 工作记忆捕获模块，负责捕获工作相关的记忆并存入 ABM
    /// </summary>
    public JobMemoryCapturer JobCapturer => _jobCapturer;

    /// <summary>
    /// 战斗记忆捕获模块，负责捕获实际执行的攻击并存入 ABM
    /// </summary>
    public CombatMemoryCapturer CombatCapturer => _combatCapturer;

    /// <summary>
    /// 机械维护器，负责衰减、清理、容量治理与直接层级迁移
    /// </summary>
    public MemoryMaintainer Maintainer => _maintainer;

    /// <summary>
    /// 语义总结器，负责每日/手动/选中总结与归档
    /// </summary>
    public MemorySummarizer Summarizer => _summarizer;

    /// <summary>
    /// 记忆仓库交互器，用于向 UI 层提供交互接口
    /// </summary>
    public MemoryInteractor Interactor => _interactor;

    // 构造函数，初始化捕获模块和维护器
    public FourLayerMemoryComp()
    {
        _jobCapturer = new JobMemoryCapturer(this);
        _combatCapturer = new CombatMemoryCapturer(this);
        _maintainer = new MemoryMaintainer(this);
        _summarizer = new MemorySummarizer(this);
        _interactor = new MemoryInteractor(this);
    }


#warning 等正式版迭代稳定后，将移除此处的向后兼容逻辑
    // 向后兼容临时字段
    private bool _summarizedIdsInitialized = true;
    private bool _alreadyUpdateSummarizationType = true;
    // 存档读写
    public override void PostExposeData()
    {
        // 存档时，清理 SummarizedIds 中已不存在于主仓库中的条目
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _summarizedIds?.IntersectWith(
                _activeMemories.Concat(_situationalMemories).Concat(_eventLogMemories).Concat(_archiveMemories)
                .Where(m => m is not null)
                .Select(m => m.OriginId)
                );
        }

        base.PostExposeData();
        Scribe_Collections.Look(ref _activeMemories, "activeMemories", LookMode.Deep); // label建议使用大写开头。但此处屎山已成
        Scribe_Collections.Look(ref _situationalMemories, "situationalMemories", LookMode.Deep);
        Scribe_Collections.Look(ref _eventLogMemories, "eventLogMemories", LookMode.Deep);
        Scribe_Collections.Look(ref _archiveMemories, "archiveMemories", LookMode.Deep);
        Scribe_Collections.Look(ref _summarizedIds, "summarizedIds", LookMode.Value);
        Scribe_Values.Look(ref _summarizedIdsInitialized, "summarizedIdsInitialized", false);
        Scribe_Values.Look(ref _alreadyUpdateSummarizationType, "AlreadyUpdateSummarizationType", false);

        // 集合空保护
        _activeMemories ??= new();
        _situationalMemories ??= new();
        _eventLogMemories ??= new();
        _archiveMemories ??= new();

        // 向后兼容
        if (Scribe.mode is LoadSaveMode.PostLoadInit)
        {
            if (!_summarizedIdsInitialized)
            {
                SummarizedIds.UnionWith(
                    _activeMemories.Concat(_situationalMemories).Concat(_eventLogMemories)
                    .Where(m => m is not null)
                    .Select(m => m.OriginId)
                    );
                _summarizedIdsInitialized = true;
            }

            if (!_alreadyUpdateSummarizationType)
            {
                foreach (var memory in EventLogMemories.Concat(ArchiveMemories))
                    memory?.Type = MemoryType.Summarization;

                _alreadyUpdateSummarizationType = true;
            }
        }
    }
    public void ExposeData() => PostExposeData();

    /// <summary>
    /// 记忆导入
    /// </summary>
    /// <remarks>
    /// 因为调整成员时需要及时同步，故作为实例方法放在组件中。
    /// PostExposeData 其实就是与 Import 对标的实例导出方法
    /// </remarks>
    public void Import(FourLayerMemoryComp importComp)
    {
        if (importComp is null) return;

        ActiveMemories.Clear();
        ActiveMemories.AddRange(importComp.ActiveMemories);

        SituationalMemories.Clear();
        SituationalMemories.AddRange(importComp.SituationalMemories);

        EventLogMemories.Clear();
        EventLogMemories.AddRange(importComp.EventLogMemories);

        ArchiveMemories.Clear();
        ArchiveMemories.AddRange(importComp.ArchiveMemories);

        SummarizedIds.Clear();
        SummarizedIds.UnionWith(importComp.SummarizedIds);
    }

    // 组件 tick
    // 主 comp 只负责控制 tick 粒度和向子 comp 下发任务
    /// <summary>
    /// 低频 Tick
    /// </summary>
    public override void CompTickRare()
    {
        base.CompTickRare();

        // 每小时 tick
        if (parent.IsHashIntervalTick(GenDate.TicksPerHour))
        {
            _summarizer.DailySummarize();

            _maintainer.RunDecay();
            _maintainer.ConvertActiveMemories();
            _maintainer.CleanupLowActivityMemories();
        }

        // 每天 tick
        if (parent.IsHashIntervalTick(GenDate.TicksPerDay))
        {
            _summarizer.PeriodicArchive();

            _maintainer.EnforceMemoryLimits();
        }
    }

    // 注入层相关，待后续分离解耦
    /// <summary>
    /// ⭐ v4.0: 更新检索逻辑
    /// - ABM: 按 conversationId 去重后返回所有
    /// - ELS/CLPA: 保持原有逻辑
    /// </summary>
    public List<MemoryEntry> RetrieveMemories(MemoryQuery query)
    {
        var results = new List<MemoryEntry>();

        // ⭐ v4.0: ABM 无容量限制，返回所有匹配的
        var abmCandidates = _activeMemories
            .Where(m => MatchesQuery(m, query))
            .OrderByDescending(m => m.GameTick);
        results.AddRange(abmCandidates);

        if (_situationalMemories.Count > 0)
        {
            var scmCandidates = _situationalMemories
                .Where(m => MatchesQuery(m, query))
                .OrderByDescending(m => m.CalculateRetrievalScore(null, query.keywords))
                .ThenBy(m => m.Id)
                .Take(5);
            results.AddRange(scmCandidates);
        }

        if (query.includeContext && results.Count < query.maxCount)
        {
            // ⭐ v3.3.2.29: ELS 候选 - 确定性排序（分数降序 + ID 升序）
            var elsCandidates = _eventLogMemories
                .Where(m => MatchesQuery(m, query))
                .OrderByDescending(m => m.CalculateRetrievalScore(null, query.keywords))
                .ThenBy(m => m.Id)
                .Take(query.maxCount - results.Count);
            results.AddRange(elsCandidates);
        }

        if (query.layer == MemoryLayer.Archive)
        {
            // ⭐ v3.3.2.29: CLPA 候选 - 确定性排序（重要性降序 + ID 升序）
            var clpaCandidates = _archiveMemories
                .Where(m => MatchesQuery(m, query))
                .OrderByDescending(m => m.Importance)
                .ThenBy(m => m.Id)
                .Take(3);
            results.AddRange(clpaCandidates);
        }

        return results.Take(query.maxCount).ToList();
    }

    private bool MatchesQuery(MemoryEntry memory, MemoryQuery query)
    {
        if (query.type.HasValue && memory.Type != query.type.Value)
            return false;

        if (query.layer.HasValue && memory.Layer != query.layer.Value)
            return false;

        if (!string.IsNullOrEmpty(query.relatedPawn) && memory.relatedPawnName != query.relatedPawn)
            return false;

        if (query.tags.Any() && !query.tags.Any(t => memory.Tags.Contains(t)))
            return false;

        return true;
    }

    // 兼容旧API：GetMemoryContext
    public string GetMemoryContext(int count = 5)
    {
        var query = new MemoryQuery
        {
            maxCount = count,
            includeContext = true
        };

        var memories = RetrieveMemories(query);
        var context = new StringBuilder();

        foreach (var memory in memories)
        {
            context.AppendLine($"- [{memory.TypeName}] {memory.Content} ({memory.AgeString})");
        }

        return context.ToString();
    }

    // 兼容旧API：GetRelevantMemories
    public List<MemoryEntry> GetRelevantMemories(int count = 5)
    {
        var query = new MemoryQuery
        {
            maxCount = count,
            includeContext = true
        };

        return RetrieveMemories(query);
    }
}
