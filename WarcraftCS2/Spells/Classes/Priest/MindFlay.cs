using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Damage.Services;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    /// Канал: каждые 0.5с наносит 6 Shadow и вешает slow 50% на 1.0с (обновляется).
    public static class MindFlay
    {
        private const string SpellId       = "priest_mind_flay";
        private const double ManaCost      = 18.0;
        private const double CooldownSec   = 3.0;

        private const double TotalSec      = 3.0;
        private const double TickSec       = 0.5;
        private const double TickDamage    = 6.0;

        private const double SlowPct       = 50.0;
        private const double SlowDuration  = 1.0;

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

            var target = Targeting.TraceEnemyByView(caster, 850f, 45f);
            if (target is null || !target.IsValid) { failReason = "Нет цели"; return false; }
            var tsid = (ulong)target.SteamID;

            return ChannelScheduler.BeginChannel(
                plugin,
                caster,
                SpellId,
                totalDurationSec: TotalSec,
                tickIntervalSec: TickSec,
                onTick: () =>
                {
                    // урон
                    plugin.WowApplyInstantDamage(sid, tsid, TickDamage, DamageSchool.Shadow);
                    // slow-метка (обновляем каждый тик)
                    plugin.WowAuras.AddOrRefresh(tsid, "slow_mindflay", AuraCategory.Slow | AuraCategory.Magic, SlowDuration, sid, magnitude: SlowPct);
                },
                onEnd: null,
                onCancel: _ => { /* notify */ },
                allowMove: false,
                moveTolerance: 12f,
                cancelOnStun: true,
                cancelOnSilence: true
            );
        }
    }
}