using System;
using System.Collections.Generic;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;

namespace wowmod_cs2.MenuSystem
{
    internal static class MenuAPI
    {
        internal static readonly Dictionary<int, MenuPlayer> Players = new();
        private static readonly Dictionary<ulong, PlayerButtons> _prevButtons = new();

        internal static void Load(BasePlugin plugin)
        {
            plugin.RegisterListener<Listeners.OnTick>(OnTick);
        }

        internal static MenuPlayer GetPlayer(CCSPlayerController player)
        {
            var slot = player.Slot;
            if (!Players.TryGetValue(slot, out var mp))
            {
                mp = new MenuPlayer { player = player };
                Players[slot] = mp;
            }
            else
            {
                mp.player = player; // на случай реконнекта
            }
            return mp;
        }

        private static void OnTick()
        {
            foreach (var p in Utilities.GetPlayers())
            {
                if (p == null || !p.IsValid || p.Connected != PlayerConnectedState.PlayerConnected)
                    continue;

                var mp   = GetPlayer(p);
                var now  = p.Buttons;
                var sid  = p.SteamID;
                var prev = _prevButtons.TryGetValue(sid, out var old) ? old : now;
                _prevButtons[sid] = now;

                if (mp.MainMenu == null) continue;

                bool Pressed(PlayerButtons f) => (prev & f) == 0 && (now & f) != 0;

                // Управление: W/S — навигация, E — выбрать, R — назад
                if (Pressed(PlayerButtons.Forward)) mp.ScrollUp();    // W
                if (Pressed(PlayerButtons.Back))    mp.ScrollDown();  // S
                if (Pressed(PlayerButtons.Use))     mp.Choose();      // E
                if (Pressed(PlayerButtons.Reload))  mp.GoBackToPrev(mp.CurrentChoice?.Value.Parent?.Prev); // R

                // Рендер
                var html = mp.CenterHtml ?? string.Empty;
                Server.NextFrame(() =>
                {
                    if (mp.player != null && mp.player.IsValid)
                        mp.player.PrintToCenterHtml(html);
                });
            }
        }
    }
}