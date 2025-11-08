using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using WarcraftCS2.Spells.Systems.Damage;

namespace wowmod_cs2;

public partial class WowmodCs2
{
    // Входящая редукция (ауры)
    private const double KingsDamageReduction      = 0.10;
    private const double ShieldWallDamageReduction = 0.60;

    // Печати (Paladin) — on-hit
    private const double SealRighteousnessBonusHoly = 6.0;
    private const double SealCommandCleaveHoly      = 7.0;
    private const float  SealCommandCleaveRadius    = 240f;
    private const int    SealCommandMaxTargets      = 2;

    // Шаман — Windfury on-hit
    private const double WindfuryBonusNature = 8.0;

    // Общий ICD для on-hit проков (Paladin Seals, Shaman Windfury)
    private const double OnHitICDSeconds = 0.25;
    private readonly Dictionary<ulong, double> _onHitLastProc = new();

    private static readonly HashSet<string> _blockedWeapons = new(StringComparer.OrdinalIgnoreCase)
    {
        "inferno","molotov","decoy","smokegrenade","flashbang"
    };

    public HookResult OnPlayerHurt(EventPlayerHurt ev, GameEventInfo info)
    {
        var victim = ev.Userid;
        var attacker = ev.Attacker;
        if (victim is null || !victim.IsValid) return HookResult.Continue;
        if (attacker is null || !attacker.IsValid || attacker == victim) return HookResult.Continue;

        // Входящая редукция (мультипликативно)
        int rawDamage = ev.DmgHealth;
        if (rawDamage > 0)
        {
            double mult = 1.0;
            var vSid = (ulong)victim.SteamID;

            if (WowAuras.Has(vSid, "paladin.blessing_kings"))
                mult *= (1.0 - KingsDamageReduction);

            if (WowAuras.Has(vSid, "warrior.shield_wall"))
                mult *= (1.0 - ShieldWallDamageReduction);

            if (mult < 1.0)
            {
                var vp = victim.PlayerPawn?.Value;
                if (vp is { IsValid: true })
                {
                    int targetDamage = (int)Math.Ceiling(rawDamage * mult);
                    int refund = Math.Max(0, rawDamage - targetDamage);
                    if (refund > 0) vp.Health = Math.Min(vp.Health + refund, 120);
                }
            }
        }

        // Блокируем не-оружейные тики
        var weapon = ev.Weapon ?? string.Empty;
        if (_blockedWeapons.Contains(weapon))
            return HookResult.Continue;

        var aSid = (ulong)attacker.SteamID;
        double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        if (_onHitLastProc.TryGetValue(aSid, out var last) && (now - last) < OnHitICDSeconds)
            return HookResult.Continue;

        bool proc = false;
            WarcraftCS2.Spells.Systems.Procs.CleaveProc.TryDuplicate(this, attacker, victim, rawDamage);


        try
        {
            var vSid = (ulong)victim.SteamID;

            // Seal of Righteousness
            if (WowAuras.Has(aSid, "paladin.seal_righteousness"))
            {
                WowApplyInstantDamage(aSid, vSid, SealRighteousnessBonusHoly, DamageSchool.Holy);
                proc = true;
            }

            // Seal of Command (cleave)
            if (WowAuras.Has(aSid, "paladin.seal_command"))
            {
                var vPawn = victim.PlayerPawn?.Value;
                if (vPawn is { IsValid: true, AbsOrigin: { } vOrigin })
                {
                    int myTeam = (int)attacker.Team;

                    var candidates = Utilities.GetPlayers()
                        .Where(p => p is { IsValid: true } &&
                                    (int)p.Team != myTeam &&
                                    p != victim &&
                                    p != attacker)
                        .Select(p => new { P = p, O = p.PlayerPawn?.Value?.AbsOrigin })
                        .Where(x => x.O is not null)
                        .Select(x => new { x.P, Dist2 = Dist2(vOrigin.X, vOrigin.Y, vOrigin.Z, x.O!.X, x.O!.Y, x.O!.Z) })
                        .Where(x => x.Dist2 <= SealCommandCleaveRadius * SealCommandCleaveRadius)
                        .OrderBy(x => x.Dist2)
                        .Take(SealCommandMaxTargets)
                        .ToList();

                    foreach (var c in candidates)
                        WowApplyInstantDamage(aSid, (ulong)c.P.SteamID, SealCommandCleaveHoly, DamageSchool.Holy);

                    if (candidates.Count > 0) proc = true;
                }
            }

            // Shaman: Windfury Weapon
            if (WowAuras.Has(aSid, "shaman.windfury_weapon"))
            {
                WowApplyInstantDamage(aSid, vSid, WindfuryBonusNature, DamageSchool.Nature);
                proc = true;
            }
        }
        catch { /* валидность/edge-case */ }

        if (proc) _onHitLastProc[aSid] = now;

        return HookResult.Continue;
    }

    private static float Dist2(float ax, float ay, float az, float bx, float by, float bz)
    {
        var dx = ax - bx; var dy = ay - by; var dz = az - bz;
        return dx * dx + dy * dy + dz * dz;
    }
}