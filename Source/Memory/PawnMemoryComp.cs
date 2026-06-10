using Verse;

namespace RimTalk.Memory
{

    /// <summary>
    /// CompProperties for PawnMemoryComp
    /// </summary>
    public class CompProperties_PawnMemory : CompProperties
    {
        public CompProperties_PawnMemory()
        {
            compClass = typeof(PawnMemoryComp);
        }
    }

    /// <summary>
    /// 兼容层，确保旧数据不会丢失
    /// </summary>
    public class PawnMemoryComp : FourLayerMemoryComp
    {

    }

}
