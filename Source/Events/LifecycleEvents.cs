using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using Microsoft.Extensions.Logging;

namespace wowmod_cs2;

public partial class WowmodCs2
{
    // player_connect — первичная инициализация профиля/состояния
    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerConnect(EventPlayerConnect @event, GameEventInfo info)
    {
        var ctrl = @event.Userid; // CCSPlayerController (может быть null на ранних стадиях)
        Logger.LogDebug("[WCS] player_connect (name={Name}, steam={Steam})",
            @event.Name, @event.Xuid);

        return HookResult.Continue;
    }

    // player_disconnect 
    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        Logger.LogDebug("[WCS] player_disconnect (name={Name}, steam={Steam}, reason={Reason})",
            @event.Name, @event.Xuid, @event.Reason);

        // TODO:
        // - снять открытые меню
        // - очистить кэш кнопок/состояний/таймеров
        // - (опционально) сохранить прогресс игрока
        return HookResult.Continue;
    }

    // player_changename — синхронизация отображаемого имени в твоих структурах
    [GameEventHandler(HookMode.Post)]
    public HookResult OnPlayerChangename(EventPlayerChangename @event, GameEventInfo info)
    {
        Logger.LogDebug("[WCS] player_changename");
        
        return HookResult.Continue;
    }
}
