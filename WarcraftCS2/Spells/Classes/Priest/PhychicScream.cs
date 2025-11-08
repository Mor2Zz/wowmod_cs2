using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    /// <summary>
    /// Psychic Scream: массовый контроль вокруг кастера (моделируем как Stun|Magic с DR).
    /// </summary>
    public static class PsychicScream
    {
        private const string SpellId     = "priest_psychic_scream";
        private const double ManaCost    = 24.0;
        private const double CooldownSec = 18.0;
        private const float  Radius      = 450f;
        private const double BaseDur     = 3.0;

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

            var enemies = Targeting.FindEnemiesInRadius(caster, Radius);
            foreach (var e in enemies)
            {
                if (e is null || !e.IsValid) continue;
                var tsid = (ulong)e.SteamID;

                var cat = AuraCategory.Stun | AuraCategory.Magic;
                var dur = plugin.WowDR.Apply(tsid, cat, BaseDur);
                if (dur <= 0) continue;

                plugin.WowAuras.AddOrRefresh(tsid, "cc_psychic_scream", cat, dur, sid);
            }
            return true;
        }
    }
}