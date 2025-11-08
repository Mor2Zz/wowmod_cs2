using System;

namespace RPG.XP;

public static class XpScaler
{
    private static XpBalanceConfig _cfg = new();

    public static void InitDefaults()
    {
        // просто берём дефолты из XpBalanceConfig — без файлов/IO
        _cfg = new XpBalanceConfig();
        AntiFarmTracker.Init(_cfg);
    }

    public static XpBalanceConfig Config => _cfg;

    public static double LevelDiffFactor(int lowLevelSide, int highLevelSide, bool attackerIsHigher)
    {
        var c = _cfg.LevelScaling;
        if (!c.Enabled) return 1.0;

        var diff = Math.Max(0, highLevelSide - lowLevelSide);
        var t = Math.Clamp(diff / Math.Max(1.0, attackerIsHigher ? c.MaxPenaltyAtDiff : c.MaxBonusAtDiff), 0, 1);
        var s = t * t * (3 - 2 * t); // smoothstep
        return attackerIsHigher
            ? Lerp(1.0, c.MaxPenaltyMultiplier, s)
            : Lerp(1.0, c.MaxBonusMultiplier, s);
    }

    public static double RoleMultiplier(PlayerRole role)
    {
        var r = _cfg.Roles;
        if (!r.Enabled) return 1.0;
        return role switch
        {
            PlayerRole.Support => r.SupportMultiplier,
            PlayerRole.Tank    => r.TankMultiplier,
            _                  => r.DpsMultiplier
        };
    }

    public static double AntiFarmPairFactor(ulong a, ulong b, double nowSec, bool damageLike)
        => AntiFarmTracker.AdjustPairFactor(a, b, nowSec, damageLike);

    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}