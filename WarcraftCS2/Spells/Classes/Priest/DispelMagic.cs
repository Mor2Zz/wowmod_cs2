using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    public static class DispelMagic
    {
        private const string SpellId     = "priest_dispel_magic";
        private const double ManaCost    = 16.0;
        private const double CooldownSec = 8.0;
        private const int    MaxCount    = 2;

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

            var target = Targeting.TraceAllyByView(caster, 900f, 45f, includeSelf: true) 
                         ?? Targeting.TraceEnemyByView(caster, 900f, 45f);
            if (target is null || !target.IsValid) { failReason = "Нет цели"; return false; }

            var tsid = (ulong)target.SteamID;
            bool sameTeam = Convert.ToInt32(target.Team) == Convert.ToInt32(caster.Team);

            // Через совместимый слой: снимаем эффекты категории Magic
            var removed = plugin.WowTryDispelCompat(tsid, AuraCategory.Magic, MaxCount, onlyBeneficialOnEnemy: !sameTeam);

            // Даже если снято 0 — считаем каст успешным (в оригинале wowmod-master так же допускается отсутствие эффектов).
            return true;
        }
    }
}