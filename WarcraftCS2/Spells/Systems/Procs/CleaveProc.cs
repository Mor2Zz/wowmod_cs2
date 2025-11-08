using System;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using WarcraftCS2.Spells.Systems.Status;
using WarcraftCS2.Spells.Systems.Damage;

namespace WarcraftCS2.Spells.Systems.Procs
{
    public static class CleaveProc
    {
        private const double WarriorCleavePercent = 0.60;
        private const float  WarriorCleaveRadius  = 220f;
        private const int    WarriorCleaveMaxTargets = 1;

        public static void TryDuplicate(wowmod_cs2.WowmodCs2 plugin, CCSPlayerController attacker, CCSPlayerController victim, double rawDamage)
        {
            if (plugin is null) return;
            if (attacker is null || victim is null) return;

            ulong aSid = (ulong)attacker.SteamID;
            if (!plugin.WowAuras.Has(aSid, "warrior.cleave")) return;

            var vPawn = victim.PlayerPawn?.Value;
            if (vPawn is not { IsValid: true } || vPawn.AbsOrigin is null) return;
            var vOrigin = vPawn.AbsOrigin;

            int myTeam = (int)attacker.Team;
            var candidates = Utilities.GetPlayers()
                .Where(p => p is { IsValid: true } &&
                            (int)p.Team != myTeam &&
                            p != victim &&
                            p != attacker)
                .Select(p => new { P = p, O = p.PlayerPawn?.Value?.AbsOrigin })
                .Where(x => x.O is not null)
                .Select(x => new { x.P, Dist2 = Dist2(vOrigin.X, vOrigin.Y, vOrigin.Z, x.O!.X, x.O!.Y, x.O!.Z) })
                .Where(x => x.Dist2 <= WarriorCleaveRadius * WarriorCleaveRadius)
                .OrderBy(x => x.Dist2)
                .Take(WarriorCleaveMaxTargets)
                .ToList();

            double copy = rawDamage * WarriorCleavePercent;
            foreach (var c in candidates)
                plugin.WowApplyInstantDamage(aSid, (ulong)c.P.SteamID, copy, DamageSchool.Physical);
        }

        private static double Dist2(double ax, double ay, double az, double bx, double by, double bz)
        {
            double dx = ax - bx, dy = ay - by, dz = az - bz;
            return dx*dx + dy*dy + dz*dz;
        }
    }
}