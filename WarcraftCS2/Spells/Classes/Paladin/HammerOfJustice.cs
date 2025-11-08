using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public static class HammerOfJustice
    {
        private const string SpellId     = "pal_hammer_of_justice";
        private const double ManaCost    = 12.0;
        private const double CooldownSec = 30.0;
        private const double BaseDur     = 3.0;

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

            var target = Targeting.TraceEnemyByView(caster, 900f, 45f);
            if (target is null || !target.IsValid) { failReason = "Нет цели"; return false; }
            var tsid = (ulong)target.SteamID;

            var cat = AuraCategory.Stun | AuraCategory.Magic;
            var dur = plugin.WowDR.Apply(tsid, cat, BaseDur);
            if (dur > 0)
                plugin.WowAuras.AddOrRefresh(tsid, "hoj_stun", cat, dur, sid);

            return true;
        }
    }
}