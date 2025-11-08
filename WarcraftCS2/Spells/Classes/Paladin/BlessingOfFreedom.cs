using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public static class BlessingOfFreedom
    {
        private const string SpellId     = "pal_blessing_of_freedom";
        private const double ManaCost    = 10.0;
        private const double CooldownSec = 18.0;
        private const double DurationSec = 8.0;

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

            var target = Targeting.TraceAllyByView(caster, 900f, 45f, includeSelf: true) ?? caster;
            var tsid = (ulong)target.SteamID;

            // моментально почистим часть маг.дебаффов (часть рут/слоу попадает сюда)
            plugin.WowTryDispelCompat(tsid, AuraCategory.Magic, maxCount: 2, onlyBeneficialOnEnemy: false);

            // метка «свободы» — дальше твой Control сервис может читать её и игнорить root/slow.
            plugin.WowAuras.AddOrRefresh(
                targetSid: tsid,
                auraId: "buff_blessing_of_freedom",
                categories: AuraCategory.Magic,
                durationSec: DurationSec,
                sourceSid: sid
            );

            return true;
        }
    }
}