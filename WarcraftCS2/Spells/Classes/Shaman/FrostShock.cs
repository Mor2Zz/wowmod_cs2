using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Spells.Classes.Shaman
{
    public class ShamanFrostShock : IActiveSpell
    {
        public string Id => "shaman.frost_shock";
        public string Name => "Frost Shock";
        public string Description => "Наносит 22 Frost урона и замедляет цель на 40% на 3с.";

        private const double ManaCost = 16.0;
        private const double CooldownSec = 6.0;
        private const double DamageAmt = 22.0;
        private static readonly TimeSpan SlowDur = TimeSpan.FromSeconds(3);

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not wowmod_cs2.WowmodCs2 plugin || player is not { IsValid: true }) return false;

            var ctx = plugin.GetWowCombatContext();
            if (!Systems.Casting.CastGate.TryBeginCast(ctx, (ulong)player.SteamID, Id, ManaCost, CooldownSec, out var reason))
            { rt.Print(player, $"[Warcraft] Frost Shock: {reason}."); return false; }

            var target = Targeting.TraceEnemyByView(player);
            if (target is null || !target.IsValid) { rt.Print(player, "[Warcraft] Frost Shock: цель не найдена."); return false; }

            var ok = plugin.WowApplyInstantDamage((ulong)player.SteamID, (ulong)target.SteamID, DamageAmt, DamageSchool.Frost);
            DebuffApi.TryApplySlow(target, SlowDur, Id);
            rt.Print(player, ok ? "[Warcraft] Frost Shock: попадание!" : "[Warcraft] Frost Shock: цель защищена.");
            return ok;
        }
    }
}