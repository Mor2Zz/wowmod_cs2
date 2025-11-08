using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    public interface ILineOfSight
    {
        bool Has(in TargetSnapshot a, in TargetSnapshot b);
    }
}
