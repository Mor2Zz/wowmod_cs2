using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public static class Cleanse
    {
        private const string SpellId     = "pal_cleanse";
        private const double ManaCost    = 14.0;
        private const double CooldownSec = 6.0;
        private const int    MaxCount    = 2;

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

            var ally = Targeting.TraceAllyByView(caster, 900f, 45f, includeSelf: true) ?? caster;
            var tsid = (ulong)ally.SteamID;

            // Снимаем Magic-эффекты (совместимо с твоим Dispel/AuraService через compat)
            plugin.WowTryDispelCompat(tsid, AuraCategory.Magic, MaxCount, onlyBeneficialOnEnemy: false);
            return true;
        }
    }
}