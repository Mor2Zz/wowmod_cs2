using System;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    [Flags]
    public enum LoSFilter
    {
        None           = 0,
        IgnoreOwner    = 1 << 0, // как AimTargetFilter: не учитывать кастера
        IgnorePlayers  = 1 << 1, // как WallTargetFilter: учитывать только мир/стены
        IgnoreFriend   = 1 << 2, // игнор союзных игроков
        IgnoreEnemy    = 1 << 3  // игнор вражеских игроков
    }

    public enum LoSMask
    {
        All,           // MASK_ALL
        PlayerSolid,   // MASK_PLAYERSOLID
        WorldOnly      // мир/геометрия (если трассер умеет)
    }

    // Расширенная линия видимости (если движок-трассер поддерживает фильтры)
    internal interface IFilteredLineOfSight : ILineOfSight
    {
        bool HasFiltered(in TargetSnapshot a, in TargetSnapshot b, LoSFilter filter, LoSMask mask);
        bool Ray(in Vector3 from, in Vector3 to, LoSFilter filter, LoSMask mask);
    }
}
