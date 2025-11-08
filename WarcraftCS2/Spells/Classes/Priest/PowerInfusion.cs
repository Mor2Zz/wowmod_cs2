using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    public static class PowerInfusion
    {
        private const string SpellId     = "priest_power_infusion";
        private const double ManaCost    = 20.0;
        private const double CooldownSec = 120.0;
        private const double DurationSec = 15.0;
        private const double Magnitude   = 20.0; // «+20% скорость/хаст» как метка

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

            var ally = WarcraftCS2.Spells.Systems.Core.Targeting.Targeting.TraceAllyByView(caster, 900f, 45f, includeSelf: true) ?? caster;
            var tsid = (ulong)ally.SteamID;

            plugin.WowAuras.AddOrRefresh(
                targetSid: tsid,
                auraId: "buff_power_infusion",
                categories: AuraCategory.Magic,
                durationSec: DurationSec,
                sourceSid: sid,
                magnitude: Magnitude
            );
            return true;
        }
    }
}