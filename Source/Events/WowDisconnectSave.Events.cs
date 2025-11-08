using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Events;

namespace wowmod_cs2
{
    public partial class WowmodCs2
    {
        // Подстраховка: сейв при выходе игрока
        [GameEventHandler]
        public HookResult _wow_OnPlayerDisconnect(EventPlayerDisconnect ev, GameEventInfo info)
        {
            try { SaveProfilesSafe(); } catch {}
            return HookResult.Continue;
        }

        // Доп. подстраховка: сейв на конец матча (если событие поддерживается)
        [GameEventHandler]
        public HookResult _wow_OnCsWinPanelMatch(EventCsWinPanelMatch ev, GameEventInfo info)
        {
            try { SaveProfilesSafe(); } catch {}
            return HookResult.Continue;
        }
    }
}