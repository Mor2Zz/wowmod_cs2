using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Rogue
{
    /// <summary>Спринт: +50% скорости на 6с (как отрицательный slow).</summary>
    public static class Sprint
    {
        private const string SpellId     = "rogue_sprint";
        private const double EnergyCost  = 0.0;  // подставишь свой ресурс
        private const double CooldownSec = 20.0;
        private const double DurationSec = 6.0;
        private const double SpeedPct    = -50.0; // отрицательное = ускорение

        public static bool TryCast(wowmod_cs2.WowmodCs2 plugin, CCSPlayerController caster, out string failReason)
        {
            failReason = string.Empty;
            if (plugin is null || caster is null || !caster.IsValid) { failReason = "Некорректный кастер"; return false; }

            var sid = (ulong)caster.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { failReason = "Вы оглушены"; return false; }
            if (plugin.WowControl.IsSilenced(sid)) { failReason = "Вы немые";    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, EnergyCost, CooldownSec, out failReason))
                return false;

            // кладём «ускоряющую» ауру (Slow с отрицательной величиной)
            plugin.WowAuras.AddOrRefresh(
                targetSid: sid,
                auraId: "buff_sprint",
                categories: AuraCategory.Magic | AuraCategory.Slow,
                durationSec: DurationSec,
                sourceSid: sid,
                magnitude: SpeedPct // -50% = +50% скорость
            );
            return true;
        }
    }
}