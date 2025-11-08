using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    public static class GreaterHeal
    {
        private const string SpellId     = "priest_greater_heal";
        private const double ManaCost    = 32.0;
        private const double CooldownSec = 8.0;
        private const double HealAmount  = 45.0;
        private const double CastTime    = 2.5;

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

            return CastScheduler.BeginCast(
                plugin,
                caster,
                SpellId,
                CastTime,
                onComplete: () => { plugin.WowApplyInstantHeal(sid, tsid, HealAmount); },
                onCancel:   _ => { /* notify */ },
                allowMove: false,
                moveTolerance: 12f,
                cancelOnStun: true,
                cancelOnSilence: true
            );
        }
    }
}