using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    public static class PowerWordBarrier
    {
        private const string SpellId      = "priest_power_word_barrier";
        private const double ManaCost     = 24.0;
        private const double CooldownSec  = 120.0;
        private const double AbsorbAmount = 100.0;
        private const double DurationSec  = 10.0;

        public static bool TryCast(WowmodCs2 plugin, CCSPlayerController caster, out string failReason)
        {
            failReason = string.Empty;
            if (plugin is null || caster is null || !caster.IsValid) { failReason = "Некорректный кастер"; return false; }

            var sid = (ulong)caster.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { failReason = "Оглушены"; return false; }
            if (plugin.WowControl.IsSilenced(sid)) { failReason = "Немой";     return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out failReason))
                return false;

            // метка для UI/логики
            plugin.WowAuras.AddOrRefresh(
                targetSid: sid,
                auraId: "shield_power_word_barrier",
                categories: AuraCategory.Magic,
                durationSec: DurationSec,
                sourceSid: sid,
                magnitude: AbsorbAmount
            );

            // фактический щит
            plugin.WowAddShieldCompat(sid, "power_word_barrier", AbsorbAmount, DurationSec, sid);
            return true;
        }
    }
}