using System.Linq;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Events;
using RPG.XP;

namespace wowmod_cs2;

public partial class WowmodCs2
{
    public HookResult OnBombBeginplant(EventBombBeginplant ev, GameEventInfo info) => HookResult.Continue;

    public HookResult OnBombAbortplant(EventBombAbortplant ev, GameEventInfo info) => HookResult.Continue;

    public HookResult OnBombPlanted(EventBombPlanted ev, GameEventInfo info)
    {
        var planter = ev.Userid;
        if (planter is { IsValid: true })
        {
            var prof = GetOrCreateProfile(planter);
            var role = RoleSystem.GetRole(planter.SteamID);
            var xp = XpRules.FromBombPlant(prof.Level, _profiles.Values.Select(p => p.Level), role);
            if (xp > 0) GiveXp(planter, xp, "plant");
        }
        return HookResult.Continue;
    }

    public HookResult OnBombDefused(EventBombDefused ev, GameEventInfo info)
    {
        var defuser = ev.Userid;
        if (defuser is { IsValid: true })
        {
            var prof = GetOrCreateProfile(defuser);
            var role = RoleSystem.GetRole(defuser.SteamID);
            var xp = XpRules.FromBombDefuse(prof.Level, _profiles.Values.Select(p => p.Level), role);
            if (xp > 0) GiveXp(defuser, xp, "defuse");
        }
        return HookResult.Continue;
    }

    public HookResult OnBombExploded(EventBombExploded ev, GameEventInfo info) => HookResult.Continue;

    public HookResult OnBombPickup(EventBombPickup ev, GameEventInfo info) => HookResult.Continue;

    public HookResult OnBombDropped(EventBombDropped ev, GameEventInfo info) => HookResult.Continue;
}