using RimWorld.Planet;
using Verse;

namespace RimTalk.Memory.AI
{
    /// <summary>
    /// AI 请求管理器 - 启动协程
    /// </summary>
    public class AIRequestManager : WorldComponent
    {
        public AIRequestManager(World world) : base(world) { }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();
            // Process a few callbacks each tick to avoid lag spikes
            IndependentAISummarizer.ProcessPendingCallbacks(5);
        }
    }
}
