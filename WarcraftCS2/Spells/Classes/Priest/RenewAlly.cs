using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    /// <summary>Renew на союзника по взгляду. Если союзника нет — повесит на себя.</summary>
    public static class RenewAlly
    {
        private const string SpellId       = "priest_renew_ally";
        private const double ManaCost      = 18.0;
        private const double CooldownSec   = 6.0;
        private const double TickHeal      = 12.0;
        private const double TickInterval  = 1.0;
        private const double DurationSec   = 6.0;

        public static bool TryCast(WowmodCs2 plugin, CCSPlayerController caster, out string failReason)
        {
            failReason = string.Empty;
            if (plugin is null || caster is null || !caster.IsValid) { failReason = "Некорректный кастер"; return false; }
            var sid = (ulong)caster.SteamID;

            if (plugin.WowControl.IsStunned(sid))  { failReason = "Вы оглушены"; return false; }
            if (plugin.WowControl.IsSilenced(sid)) { failReason = "Вы немые";    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out failReason))
                return false;

            var ally = Targeting.TraceAllyByView(caster, 900f, 45f, includeSelf: true) ?? caster;
            var tsid = (ulong)ally.SteamID;

            plugin.WowPeriodic.AddOrRefreshHot(
                casterSid: sid,
                targetSid: tsid,
                id: "renew",
                amountPerTick: TickHeal,
                intervalSec: TickInterval,
                durationSec: DurationSec
            );

            return true;
        }
    }
}