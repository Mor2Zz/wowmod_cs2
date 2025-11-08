using System;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Spells.Classes.Shaman
{
    public class ShamanFlameShock : IActiveSpell
    {
        public string Id => "shaman.flame_shock";
        public string Name => "Flame Shock";
        public string Description => "Накладывает на цель DoT: 6 Fire урона/сек на 10с.";

        private const double ManaCost = 18.0;
        private const double CooldownSec = 5.0;
        private const double DurationSec = 10.0;
        private const double TickEverySec = 1.0;
        private const double TickDamage = 6.0;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not wowmod_cs2.WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, (ulong)player.SteamID, Id, ManaCost, CooldownSec, out var reason))
            { rt.Print(player, $"[Warcraft] Flame Shock: {reason}."); return false; }

            var target = Targeting.TraceEnemyByView(player);
            if (target is null || !target.IsValid) { rt.Print(player, "[Warcraft] Flame Shock: цель не найдена."); return false; }

            var tsid = (ulong)target.SteamID;
            var asid = (ulong)player.SteamID;

            plugin.WowAuras.AddOrRefresh(tsid, Id, AuraCategory.Magic, DurationSec, asid);

            double end = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0 + DurationSec;

            void Tick()
            {
                if (!plugin.WowAuras.Has(tsid, Id)) return;
                double now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
                if (now > end) return;

                plugin.WowApplyInstantDamage(asid, tsid, TickDamage, DamageSchool.Fire);
                plugin.AddTimer((float)TickEverySec, Tick);
            }

            plugin.AddTimer((float)TickEverySec, Tick);
            rt.Print(player, "[Warcraft] Flame Shock: наложен.");
            return true;
        }
    }
}