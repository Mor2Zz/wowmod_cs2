using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using CssTimer = CounterStrikeSharp.API.Modules.Timers.Timer;

using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Damage.Resists;
using WarcraftCS2.Spells.Systems.Damage.Services;

using WarcraftCS2.Spells.Systems.Status;
using WarcraftCS2.Spells.Systems.Status.Periodic;

using DamagePipelineCore = WarcraftCS2.Spells.Systems.Damage.DamagePipeline;

// events
using CounterStrikeSharp.API.Modules.Events;

namespace wowmod_cs2
{
    public partial class WowmodCs2
    {
        private CssTimer? _wow_damageSweepTimer;

        public DamagePipelineCore WowDamagePipeline { get; private set; } = null!;
        public ImmunityService WowImmunity { get; private set; } = null!;
        public ShieldService WowShields { get; private set; } = null!;
        public DispelService WowDispel { get; private set; } = null!;
        public AuraService WowAuras { get; private set; } = null!;
        public ControlService WowControl { get; private set; } = null!;
        public PeriodicService WowPeriodic { get; private set; } = null!;
        public DiminishingReturnsService WowDR { get; private set; } = null!;
        public ResistService WowResists { get; private set; } = null!;

        // Mortal Strike anti-heal baseline
        private const double MortalStrikeAntiHeal01 = 0.40;

        // MS: ExPrecision → метка цели на 4с (victimSid -> (attackerSid, until))
        private readonly ConcurrentDictionary<ulong, (ulong attackerSid, DateTime until)> _msExPrecisionMarks = new();

        // Bulwark: Spellbreaker → маг-только абсорб пул на цели (sid -> queue of pools)
        private sealed record AbsorbPool(double AmountLeft, DateTime Until);
        private readonly ConcurrentDictionary<ulong, ConcurrentQueue<AbsorbPool>> _magicAbsorb = new();

        private void _wow_InitDamageServices()
        {
            WowImmunity = new ImmunityService();
            WowShields  = new ShieldService();
            WowDispel   = new DispelService();
            WowAuras    = new AuraService(WowDispel);
            WowControl  = new ControlService(WowAuras);
            WowPeriodic = new PeriodicService();
            WowDR       = new DiminishingReturnsService();
            WowResists  = new ResistService();

            WowDamagePipeline = new DamagePipelineCore(WowImmunity, WowShields);

            // обработчик смерти для MS:ex_precision (название уникальное, чтобы не конфликтовать)
            RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath_ExPrecision);

            // 1 Гц тик: свип эффектов и периодика
            void ScheduleSweep()
            {
                _wow_damageSweepTimer = AddTimer(1.0f, () =>
                {
                    try
                    {
                        var now = DateTime.UtcNow;

                        WowImmunity.Sweep(now);
                        WowShields.Sweep(now);
                        WowAuras.Sweep(now);
                        WowDispel.Sweep(now);
                        WowDR.Sweep(now);

                        // чистим просроченные метки ExPrecision
                        foreach (var kv in _msExPrecisionMarks)
                        {
                            if (kv.Value.until <= now) _msExPrecisionMarks.TryRemove(kv.Key, out _);
                        }

                        // чистим просроченные маг-пулы
                        foreach (var kv in _magicAbsorb)
                        {
                            var q = kv.Value;
                            while (q.TryPeek(out var head) && head.Until <= now) q.TryDequeue(out _);
                        }

                        WowPeriodic.Tick(
                            now,
                            isValidSid: _wow_IsOnline,
                            applyDamage: _wow_ApplyPeriodicDamage,
                            applyHeal: _wow_ApplyPeriodicHeal
                        );

                        foreach (var p in Utilities.GetPlayers())
                        {
                            if (p is null || !p.IsValid) continue;
                            var sid = (ulong)p.SteamID;
                            var state = WowControl.GetMovementState(sid);
                            OnMovementStateComputed(sid, state);
                        }
                    }
                    catch { /* no-op */ }

                    ScheduleSweep();
                });
            }
            ScheduleSweep();

            Logger?.LogInformation("[wowmod] Damage services up: Immunity/Shields/Dispel/Auras/Control/Periodic/DR/Resists/Pipeline + ex_precision/magic_absorb hooks");
        }

        // ===== Helpers: players =====
        private CCSPlayerController? _wow_TryGetPlayer(ulong sid)
        {
            foreach (var p in Utilities.GetPlayers())
            {
                if (p is null || !p.IsValid) continue;
                if ((ulong)p.SteamID == sid) return p;
            }
            return null;
        }

        private bool _wow_IsOnline(ulong sid) => _wow_TryGetPlayer(sid) is not null;

        // ===== Periodic core hooks =====
        private bool _wow_ApplyPeriodicDamage(ulong casterSid, ulong targetSid, double amount, DamageSchool school)
        {
            var atk = _wow_TryGetPlayer(casterSid);
            var vic = _wow_TryGetPlayer(targetSid);
            if (atk is null || vic is null) return false;

            amount = ConsumeMagicAbsorbIfAny(targetSid, school, amount);

            var afterResist = WowResists.Apply(targetSid, school, amount);
            if (afterResist <= 0) return true;

            if (!WowDamagePipeline.ResolveIncoming(casterSid, targetSid, afterResist, school,
                    out var final, out var absorbed, out var reason))
                return false;

            OnPeriodicDamageResolved(casterSid, targetSid, final, absorbed, school, reason);
            return true;
        }

        private bool _wow_ApplyPeriodicHeal(ulong casterSid, ulong targetSid, double amount)
        {
            amount = ApplyAntiHealIfAny(targetSid, amount);
            OnPeriodicHealResolved(casterSid, targetSid, amount);
            return true;
        }

        // ===== Instant helpers =====
        public bool WowApplyInstantDamage(ulong casterSid, ulong targetSid, double baseAmount, DamageSchool school)
        {
            var atk = _wow_TryGetPlayer(casterSid);
            var vic = _wow_TryGetPlayer(targetSid);
            if (atk is null || vic is null) return false;

            var amount = ConsumeMagicAbsorbIfAny(targetSid, school, baseAmount);

            var afterResist = WowResists.Apply(targetSid, school, amount);
            if (afterResist <= 0)
            {
                OnInstantDamageResolved(casterSid, targetSid, 0.0, 0.0, school, "resisted");
                return true;
            }

            if (!WowDamagePipeline.ResolveIncoming(casterSid, targetSid, afterResist, school,
                    out var final, out var absorbed, out var reason))
                return false;

            OnInstantDamageResolved(casterSid, targetSid, final, absorbed, school, reason);
            return true;
        }

        public bool WowApplyInstantHeal(ulong casterSid, ulong targetSid, double amount)
        {
            amount = ApplyAntiHealIfAny(targetSid, amount);
            var caster = _wow_TryGetPlayer(casterSid);
            var target = _wow_TryGetPlayer(targetSid);
            if (caster is null || target is null) return false;
            OnInstantHealResolved(casterSid, targetSid, amount);
            return true;
        }

        public (DamagePipelineCore pipeline, ImmunityService immune, ShieldService shields, DispelService dispel, AuraService auras)
            GetWowDamageServices() => (WowDamagePipeline, WowImmunity, WowShields, WowDispel, WowAuras);

        partial void OnPeriodicDamageResolved(ulong casterSid, ulong targetSid, double finalDamage, double absorbed,
            DamageSchool school, string? reason);

        partial void OnPeriodicHealResolved(ulong casterSid, ulong targetSid, double amount);

        partial void OnMovementStateComputed(ulong sid, MovementState state);

        partial void OnInstantDamageResolved(ulong casterSid, ulong targetSid, double finalDamage, double absorbed,
            DamageSchool school, string? reason);

        partial void OnInstantHealResolved(ulong casterSid, ulong targetSid, double amount);

        // ===== Mortal Strike anti-heal =====
        private double ApplyAntiHealIfAny(ulong targetSid, double amount)
        {
            try
            {
                var auras = WowAuras.GetAll(targetSid);
                foreach (var a in auras)
                {
                    if (a.AuraId == "warrior.mortal_strike.antiheal" || a.AuraId == "Warrior.MortalStrike.MS")
                    {
                        var red = a.Magnitude;
                        if (double.IsNaN(red)) red = MortalStrikeAntiHeal01;
                        if (red > 1.0) red = red / 100.0;
                        red = Math.Clamp(red, 0.0, 1.0);
                        return amount * (1.0 - red);
                    }
                }
            }
            catch { }
            return amount;
        }

        // ===== MS: ex_precision mark API (вызывается спеллом) =====
        internal void AddMsExPrecisionMark(ulong victimSid, ulong attackerSid, double durationSec)
        {
            _msExPrecisionMarks[victimSid] = (attackerSid, DateTime.UtcNow.AddSeconds(durationSec));
            // дубль-аура для визуала/совместимости
            WowAuras.AddOrRefresh(
                targetSid: victimSid,
                auraId: "warrior.mortal_strike.ex_precision.mark",
                categories: AuraCategory.None,
                durationSec: durationSec,
                sourceSid: attackerSid,
                addStacks: 1,
                maxStacks: 1,
                mode: AuraRefreshMode.RefreshDuration_KeepStacks,
                magnitude: 1.0
            );
        }

        // Событие смерти: рефанд 50% КД MS, если килл сделан обладателем метки в окне 4с
        private HookResult OnPlayerDeath_ExPrecision(EventPlayerDeath ev, GameEventInfo info)
        {
            try
            {
                var victim = ev.Userid;
                var attacker = ev.Attacker;

                if (victim is null || attacker is null) return HookResult.Continue;
                if (!victim.IsValid || !attacker.IsValid) return HookResult.Continue;

                var vsid = (ulong)victim.SteamID;
                var asid = (ulong)attacker.SteamID;

                if (_msExPrecisionMarks.TryGetValue(vsid, out var mark))
                {
                    if (mark.attackerSid == asid && mark.until >= DateTime.UtcNow)
                    {
                        _msExPrecisionMarks.TryRemove(vsid, out _);

                        // Переустановим КД MS на 50% от базового (7.0с)
                        var ctx = GetWowCombatContext();
                        ctx.Cooldowns.Start(asid, "warrior.mortal_strike", 0.5 * 7.0);
                        attacker.PrintToChat("[Warcraft] Mortal Strike: возврат 50% КД (Ex. Precision).");
                    }
                }
            }
            catch { }
            return HookResult.Continue;
        }

        // ===== Bulwark: маг-только абсорб пул =====
        internal void AddMagicAbsorb(ulong targetSid, double amount, double durationSec)
        {
            var q = _magicAbsorb.GetOrAdd(targetSid, _ => new ConcurrentQueue<AbsorbPool>());
            q.Enqueue(new AbsorbPool(amount, DateTime.UtcNow.AddSeconds(durationSec)));
        }

        private static bool IsMagic(DamageSchool s) => s != DamageSchool.Physical;

        private double ConsumeMagicAbsorbIfAny(ulong targetSid, DamageSchool school, double amount)
        {
            if (!IsMagic(school) || amount <= 0) return amount;

            if (!_magicAbsorb.TryGetValue(targetSid, out var q) || q.IsEmpty) return amount;

            var now = DateTime.UtcNow;
            double left = amount;

            // списываем из очереди пулов
            while (left > 0 && q.TryPeek(out var head))
            {
                if (head.Until <= now) { q.TryDequeue(out _); continue; }

                var take = Math.Min(left, head.AmountLeft);
                left -= take;

                var remaining = head.AmountLeft - take;
                q.TryDequeue(out _);
                if (remaining > 0)
                {
                    q.Enqueue(new AbsorbPool(remaining, head.Until));
                }
            }

            return left <= 0 ? 0 : left;
        }
    }
}