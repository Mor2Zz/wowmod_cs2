using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Rogue
{
    public static class CloakOfShadows
    {
        private const string SpellId     = "rogue_cloak_of_shadows";
        private const double EnergyCost  = 0.0;
        private const double CooldownSec = 60.0;
        private const double DurationSec = 5.0;

        public static bool TryCast(WowmodCs2 plugin, CCSPlayerController caster, out string failReason)
        {
            failReason = string.Empty;
            if (plugin is null || caster is null || !caster.IsValid) { failReason = "Некорректный кастер"; return false; }

            var sid = (ulong)caster.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { failReason = "Оглушены"; return false; }
            if (plugin.WowControl.IsSilenced(sid)) { failReason = "Немой";     return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, EnergyCost, CooldownSec, out failReason))
                return false;

            // моментальная очистка магических дебаффов
            plugin.WowTryDispelCompat(sid, AuraCategory.Magic, maxCount: 6, onlyBeneficialOnEnemy: false);

            // метка-аура
            plugin.WowAuras.AddOrRefresh(
                targetSid: sid,
                auraId: "cloak_of_shadows",
                categories: AuraCategory.Magic,
                durationSec: DurationSec,
                sourceSid: sid
            );

            // иммунитет ко всем маг. школам на время действия
            plugin.WowAddAllMagicalImmunityCompat(sid, DurationSec, sid);
            return true;
        }
    }
}