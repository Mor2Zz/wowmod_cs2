using System.Linq;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Events;
using RPG.XP;
using Microsoft.Extensions.Logging;

namespace wowmod_cs2;

public partial class WowmodCs2
{
    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        Logger.LogDebug("[WCS] round_start (timelimit={Timelimit}, objective={Objective})",
            @event.Timelimit, @event.Objective);
        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnRoundEnd(EventRoundEnd @event, GameEventInfo info)
    {
        Logger.LogDebug("[WCS] round_end (winner={Winner}, reason={Reason})",
            @event.Winner, @event.Reason);

        int winnerTeam = @event.Winner;
        var allLevels = _profiles.Values.Select(p => p.Level);

        foreach (var p in CounterStrikeSharp.API.Utilities.GetPlayers())
        {
            if (p is not { IsValid: true }) continue;
            var prof = GetOrCreateProfile(p);
            var role = RoleSystem.GetRole(p.SteamID);

            // CS2: 2 = T, 3 = CT, прочее игнор
            bool win = p.TeamNum == winnerTeam && (winnerTeam == 2 || winnerTeam == 3);
            int xp = XpRules.FromRoundResult(win, prof.Level, allLevels, role);
            if (xp > 0) GiveXp(p, xp, win ? "round win" : "round loss");
        }

        return HookResult.Continue;
    }

    [GameEventHandler(HookMode.Post)]
    public HookResult OnGameNewmap(EventGameNewmap @event, GameEventInfo info)
    {
        Logger.LogInformation("[WCS] game_newmap");
        return HookResult.Continue;
    }
}