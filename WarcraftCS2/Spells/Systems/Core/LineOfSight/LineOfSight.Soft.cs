using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    public static partial class LineOfSight
    {
        public static bool Soft(in TargetSnapshot caster, in TargetSnapshot target)
        {
            if (!target.Alive) return false;
            return true;
        }
    }
}