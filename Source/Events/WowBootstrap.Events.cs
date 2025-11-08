using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Events;

namespace wowmod_cs2
{
    public partial class WowmodCs2
    {
        [GameEventHandler]
        public HookResult _wow_Bootstrap_OnConnectFull(EventPlayerConnectFull ev, GameEventInfo info)
        {
            EnsureBootstrap();
            return HookResult.Continue;
        }

        [GameEventHandler]
        public HookResult _wow_Bootstrap_OnRoundStart(EventRoundStart ev, GameEventInfo info)
        {
            EnsureBootstrap();
            return HookResult.Continue;
        }
    }
}