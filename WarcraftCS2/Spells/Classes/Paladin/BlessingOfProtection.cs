using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Damage.Services; 
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public static class BlessingOfProtection
    {
        private const string SpellId     = "pal_blessing_of_protection";
        private const double ManaCost    = 22.0;
        private const double CooldownSec = 90.0;
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

            // 1) Метка-аура (для UI/диспела)
            plugin.WowAuras.AddOrRefresh(tsid, "bop", AuraCategory.Magic, DurationSec, sid);

            // 2) Фактический иммунитет к физике (через compat)
            plugin.WowAddPhysicalImmunityCompat(tsid, DurationSec, sid);

            return true;
        }
    }
}