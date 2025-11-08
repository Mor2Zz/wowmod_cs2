using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;

namespace wowmod_cs2;

public partial class WowmodCs2
{
    // player_shoot — триггеры "по выстрелу" (проки/счётчики/кд)
    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerShoot(EventPlayerShoot e, GameEventInfo info)
    {
        var ctrl = e.Userid; // CCSPlayerController
        // Xuid у shoot-события в дампе нет — возьмём из контроллера, если он жив
        var xuid = ctrl?.SteamID ?? 0UL;
        if (xuid != 0 && Throttled(_shootTs, xuid, 80)) return HookResult.Continue;

        Logger.LogTrace("[WCS] player_shoot (weapon={Weapon}, mode={Mode})", e.Weapon, e.Mode);

        return HookResult.Continue;
    }

    // player_footstep — механики "на шаг" (ауры/стелс) с аккуратным троттлингом
    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerFootstep(EventPlayerFootstep e, GameEventInfo info)
    {
        var ctrl = e.Userid; // CCSPlayerController
        var xuid = ctrl?.SteamID ?? 0UL;
        if (xuid != 0 && Throttled(_footstepTs, xuid, 150)) return HookResult.Continue;

        return HookResult.Continue;
    }
}