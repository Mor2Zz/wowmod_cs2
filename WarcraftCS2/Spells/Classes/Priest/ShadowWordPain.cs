using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    /// <summary>
    /// SW:P — безопасная реализация через AddTimer (тик урона), чтобы не зависеть от конкретного API PeriodicService.
    /// </summary>
    public static class ShadowWordPain
    {
        private const string SpellId     = "priest_shadow_word_pain";
        private const double ManaCost    = 10.0;
        private const double CooldownSec = 4.0;

        private const double DurationSec = 12.0;
        private const double TickInterval= 1.0;
        private const double TickDamage  = 6.0;

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

            // метка-аура (для UI/диспела)
            plugin.WowAuras.AddOrRefresh(
                targetSid: tsid,
                auraId: "dot_shadow_word_pain",
                categories: AuraCategory.Magic,
                durationSec: DurationSec,
                sourceSid: sid,
                magnitude: TickDamage
            );

            // тики урона
            var start = DateTime.UtcNow;
            void Tick()
            {
                // проверки живости/длительности
                if (target is null || !target.IsValid) return;
                var elapsed = (DateTime.UtcNow - start).TotalSeconds;
                if (elapsed >= DurationSec) return;

                plugin.WowApplyInstantDamage(sid, tsid, TickDamage, DamageSchool.Shadow);

                plugin.AddTimer((float)TickInterval, Tick);
            }
            plugin.AddTimer((float)TickInterval, Tick);

            return true;
        }
    }
}