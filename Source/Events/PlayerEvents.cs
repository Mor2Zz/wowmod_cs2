using System;
using System.Linq;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using RPG.XP;

namespace wowmod_cs2;

public partial class WowmodCs2
{
    public HookResult OnPlayerDeath(EventPlayerDeath ev, GameEventInfo info)
    {
        var victim   = ev.Userid;
        var attacker = ev.Attacker;
        var assister = ev.Assister;

        double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        // ---- KILL ----
        if (attacker is not null && attacker.IsValid && attacker != victim)
        {
            int aLvl = GetOrCreateProfile(attacker).Level;
            int vLvl = GetOrCreateProfile(victim!).Level;
            var allLvls = _profiles.Values.Select(p => p.Level);
            var roleA = RoleSystem.GetRole(attacker.SteamID);

            int xpKill = XpRules.FromKill(ev.Headshot, aLvl, vLvl, allLvls,
                                          attacker.SteamID, victim!.SteamID, now, roleA);
            if (xpKill > 0) GiveXp(attacker, xpKill, ev.Headshot ? "kill_hs" : "kill");
        }

        // ---- ASSIST ----
        if (assister is not null && assister.IsValid && assister != victim && assister != attacker)
        {
            int asLvl = GetOrCreateProfile(assister).Level;
            int vLvl  = GetOrCreateProfile(victim!).Level;
            var allLvls = _profiles.Values.Select(p => p.Level);
            var roleAs = RoleSystem.GetRole(assister.SteamID);

            int xpAssist = XpRules.FromAssist(asLvl, vLvl, allLvls,
                                              assister.SteamID, victim!.SteamID, now, roleAs);
            if (xpAssist > 0) GiveXp(assister, xpAssist, "assist");
        }

        return HookResult.Continue;
    }
}