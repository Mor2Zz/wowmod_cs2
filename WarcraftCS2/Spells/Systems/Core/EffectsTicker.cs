using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Listeners;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Spells.Systems.Core;
    /// <summary>
    /// Простой тикер эффектов (1 Гц): poison/ignite/bleed наносят периодический урон.
    /// Регистрируется из основного плагина через Register(this)/Unregister(this).
    /// </summary>
    public static class EffectsTicker
    {
        private static DateTime _next = DateTime.MinValue;

        // Подписка/отписка — вызывать из твоего BasePlugin
        public static void Register(BasePlugin plugin)
        {
            plugin.RegisterListener<Listeners.OnTick>(OnTick);
        }

        public static void Unregister(BasePlugin plugin)
        {
            // В CSS есть RemoveListener<T>, DeregisterListener нет
            plugin.RemoveListener<Listeners.OnTick>(OnTick);
        }

        // Сигнатура Listeners.OnTick — без аргументов
        private static void OnTick()
        {
            var now = DateTime.UtcNow;
            if (now < _next) return;
            _next = now.AddSeconds(1); // 1 раз в секунду

            foreach (var p in Utilities.GetPlayers())
            {
                if (p == null || !p.IsValid) continue;
                var pawn = p.PlayerPawn?.Value;
                if (pawn is null) continue;

                var sid = p.SteamID;

                // Полный иммун
                if (Buffs.Has(sid, "paladin.divine_shield")) continue;

                int dmg = 0;

                foreach (var key in Buffs.GetActive(sid))
                {
                    if      (key.StartsWith("poison.", StringComparison.OrdinalIgnoreCase)) dmg += 2;
                    else if (key.StartsWith("ignite.", StringComparison.OrdinalIgnoreCase)) dmg += 3;
                    else if (key.StartsWith("bleed.",  StringComparison.OrdinalIgnoreCase)) dmg += 2;
                }

                if (dmg <= 0) continue;

                int before = pawn.Health;
                int after  = Math.Max(1, before - dmg);
                if (after != before)
                    pawn.Health = after;
            }
        }
    }