using System;
using System.Collections.Generic;
using RPG.XP;

namespace RPG.XP;

public static class XpRules
{
    private static XpBalanceConfig C => XpScaler.Config;

    public static int FromDamage(
        double damage,
        int attackerLevel,
        int victimLevel,
        IEnumerable<int> allLevels,
        ulong attackerId,
        ulong victimId,
        double nowSec,
        PlayerRole role)
    {
        if (damage <= 0) return 0;

        double baseXp = (damage / 100.0) * C.Gains.DamagePer100;

        var levelFactor = attackerLevel >= victimLevel
            ? XpScaler.LevelDiffFactor(victimLevel, attackerLevel, attackerIsHigher: true)
            : XpScaler.LevelDiffFactor(attackerLevel, victimLevel, attackerIsHigher: false);

        var antiFarm = XpScaler.AntiFarmPairFactor(attackerId, victimId, nowSec, damageLike: true);
        var roleMul  = XpScaler.RoleMultiplier(role);

        var xp = baseXp * levelFactor * antiFarm * roleMul;
        return (int)Math.Round(Math.Max(0, xp));
    }

    public static int FromHeal(
        double heal,
        int healerLevel,
        int targetLevel,
        IEnumerable<int> allLevels,
        ulong healerId,
        ulong targetId,
        double nowSec,
        PlayerRole role)
    {
        if (heal <= 0) return 0;

        double baseXp = (heal / 100.0) * C.Gains.HealPer100;

        var levelFactor = healerLevel >= targetLevel
            ? XpScaler.LevelDiffFactor(targetLevel, healerLevel, attackerIsHigher: true)
            : XpScaler.LevelDiffFactor(healerLevel, targetLevel, attackerIsHigher: false);

        var antiFarm = XpScaler.AntiFarmPairFactor(healerId, targetId, nowSec, damageLike: false);
        var roleMul  = XpScaler.RoleMultiplier(role);

        var xp = baseXp * levelFactor * antiFarm * roleMul;
        return (int)Math.Round(Math.Max(0, xp));
    }

    public static int FromKill(
        bool headshot,
        int attackerLevel,
        int victimLevel,
        IEnumerable<int> allLevels,
        ulong attackerId,
        ulong victimId,
        double nowSec,
        PlayerRole role)
    {
        double baseXp = XpScaler.Config.Gains.KillBase + (headshot ? XpScaler.Config.Gains.HeadshotBonus : 0);

        var levelFactor = attackerLevel >= victimLevel
            ? XpScaler.LevelDiffFactor(victimLevel, attackerLevel, attackerIsHigher: true)
            : XpScaler.LevelDiffFactor(attackerLevel, victimLevel, attackerIsHigher: false);

        var antiFarm = XpScaler.AntiFarmPairFactor(attackerId, victimId, nowSec, damageLike: true);
        var roleMul  = XpScaler.RoleMultiplier(role);

        var xp = baseXp * levelFactor * antiFarm * roleMul;
        return (int)Math.Round(Math.Max(0, xp));
    }

    public static int FromAssist(
        int assisterLevel,
        int victimLevel,
        IEnumerable<int> allLevels,
        ulong assisterId,
        ulong victimId,
        double nowSec,
        PlayerRole role)
    {
        double baseXp = XpScaler.Config.Gains.AssistBase;

        var levelFactor = assisterLevel >= victimLevel
            ? XpScaler.LevelDiffFactor(victimLevel, assisterLevel, attackerIsHigher: true)
            : XpScaler.LevelDiffFactor(assisterLevel, victimLevel, attackerIsHigher: false);

        var antiFarm = XpScaler.AntiFarmPairFactor(assisterId, victimId, nowSec, damageLike: true);
        var roleMul  = XpScaler.RoleMultiplier(role);

        var xp = baseXp * levelFactor * antiFarm * roleMul;
        return (int)Math.Round(Math.Max(0, xp));
    }
}