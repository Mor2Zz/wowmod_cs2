using System;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Канальные спеллы (Damage/Heal/Shield/Accumulate) с опциональными авто-отменами.
    public static class Channel
    {
        // =========================
        //      DAMAGE CHANNEL
        // =========================
        public sealed class DamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  TickAmount;
            public float  Duration;
            public float  TickEvery = 0.5f;
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            /// Доп. произвольное условие отмены (проверяется на каждом шаге)
            public Func<bool>? ExtraCancel;

            /// Автокэнсел по контролю/урону
            public bool BreakOnControlCaster = true;
            public bool BreakOnControlTarget = false;
            public bool BreakOnDamageCaster  = false;
            public bool BreakOnDamageTarget  = false;
        }

        public static SpellResult Damage(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, DamageConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);
            ulong csidU = (ulong)csid;
            ulong tsidU = (ulong)tsid;

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            // как у тебя: один раз считаем резист и фиксируем тик
            var resist  = rt.GetResist01(tsid, cfg.School);
            var tickDmg = MathF.Max(0, cfg.TickAmount * (1f - resist));

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            // флаги автокэнсела + подписки
            bool cancelRequested = false;
            IDisposable? subCtlCaster = null, subCtlTarget = null, subDmgCaster = null, subDmgTarget = null;

            if (cfg.BreakOnControlCaster)
                subCtlCaster = ProcBus.SubscribeControlApply(a => { if (a.TgtSid == csidU) cancelRequested = true; });
            if (cfg.BreakOnControlTarget)
                subCtlTarget = ProcBus.SubscribeControlApply(a => { if (a.TgtSid == tsidU) cancelRequested = true; });

            if (cfg.BreakOnDamageCaster)
                subDmgCaster = ProcBus.SubscribeDamage(d => { if (d.TgtSid == csidU) cancelRequested = true; });
            if (cfg.BreakOnDamageTarget)
                subDmgTarget = ProcBus.SubscribeDamage(d => { if (d.TgtSid == tsidU) cancelRequested = true; });

            Func<bool> isCancelled = () =>
            {
                if (cancelRequested) return true;
                if (cfg.ExtraCancel != null && cfg.ExtraCancel()) return true;
                if (!rt.IsAlive(caster) || !rt.IsAlive(target)) return true;
                return false;
            };

            rt.StartChannel(
                csid, cfg.SpellId, MathF.Max(0.05f, cfg.Duration), MathF.Max(0.05f, cfg.TickEvery),
                isCancelled: isCancelled,
                onTick: () =>
                {
                    if (tickDmg <= 0f) return;
                    rt.DealDamage(csid, tsid, cfg.SpellId, tickDmg, cfg.School);

                    // события тика
                    ProcBus.PublishPeriodicTick(new ProcBus.PeriodicTickArgs(cfg.SpellId, csidU, tsidU, tickDmg, "channel:damage"));
                    ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, csidU, tsidU, tickDmg, cfg.School));
                },
                onEnd: () =>
                {
                    subCtlCaster?.Dispose();
                    subCtlTarget?.Dispose();
                    subDmgCaster?.Dispose();
                    subDmgTarget?.Dispose();
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // =========================
        //        HEAL CHANNEL
        // =========================
        public sealed class HealConfig
        {
            public int   SpellId;
            public float TickAmount;
            public float Duration;
            public float TickEvery = 0.5f;
            public float Mana = 0;
            public float Gcd = 0;
            public float Cooldown = 0;

            public Func<bool>? ExtraCancel;
            public bool BreakOnControlCaster = true;
            public bool BreakOnControlTarget = false;
            public bool BreakOnDamageCaster  = false;
            public bool BreakOnDamageTarget  = false;
        }

        public static SpellResult Heal(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, HealConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);
            ulong csidU = (ulong)csid;
            ulong tsidU = (ulong)tsid;

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var tickHeal = MathF.Max(0, cfg.TickAmount);

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            bool cancelRequested = false;
            IDisposable? subCtlCaster = null, subCtlTarget = null, subDmgCaster = null, subDmgTarget = null;

            if (cfg.BreakOnControlCaster)
                subCtlCaster = ProcBus.SubscribeControlApply(a => { if (a.TgtSid == csidU) cancelRequested = true; });
            if (cfg.BreakOnControlTarget)
                subCtlTarget = ProcBus.SubscribeControlApply(a => { if (a.TgtSid == tsidU) cancelRequested = true; });

            if (cfg.BreakOnDamageCaster)
                subDmgCaster = ProcBus.SubscribeDamage(d => { if (d.TgtSid == csidU) cancelRequested = true; });
            if (cfg.BreakOnDamageTarget)
                subDmgTarget = ProcBus.SubscribeDamage(d => { if (d.TgtSid == tsidU) cancelRequested = true; });

            Func<bool> isCancelled = () =>
            {
                if (cancelRequested) return true;
                if (cfg.ExtraCancel != null && cfg.ExtraCancel()) return true;
                if (!rt.IsAlive(caster) || !rt.IsAlive(target)) return true;
                return false;
            };

            rt.StartChannel(
                csid, cfg.SpellId, MathF.Max(0.05f, cfg.Duration), MathF.Max(0.05f, cfg.TickEvery),
                isCancelled: isCancelled,
                onTick: () =>
                {
                    if (tickHeal <= 0f) return;
                    rt.Heal(csid, tsid, cfg.SpellId, tickHeal);

                    ProcBus.PublishPeriodicTick(new ProcBus.PeriodicTickArgs(cfg.SpellId, csidU, tsidU, tickHeal, "channel:heal"));
                    ProcBus.PublishHeal(new ProcBus.HealArgs(cfg.SpellId, csidU, tsidU, tickHeal));
                },
                onEnd: () =>
                {
                    subCtlCaster?.Dispose();
                    subCtlTarget?.Dispose();
                    subDmgCaster?.Dispose();
                    subDmgTarget?.Dispose();
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // =========================
        //       SHIELD CHANNEL
        // =========================
        public sealed class ShieldConfig
        {
            public int    SpellId;
            public string Tag = "shield";
            public float  TickCapacity;         // сколько добавляем/накатываем за тик
            public float  Duration;             // длительность самого канала
            public float  TickEvery = 0.5f;     // частота тиков

            public float  ReapplyDuration = 0.75f; // на сколько секунд вешать щит при каждом тике (поддерживать «живым»)
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public Func<bool>? ExtraCancel;
            public bool BreakOnControlCaster = true;
            public bool BreakOnControlTarget = false;
            public bool BreakOnDamageCaster  = false;
            public bool BreakOnDamageTarget  = false;
        }

        public static SpellResult Shield(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, ShieldConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);
            ulong csidU = (ulong)csid;
            ulong tsidU = (ulong)tsid;

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var capPerTick = MathF.Max(1, cfg.TickCapacity);
            var reapplyDur = MathF.Max(0.05f, cfg.ReapplyDuration);

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            bool cancelRequested = false;
            IDisposable? subCtlCaster = null, subCtlTarget = null, subDmgCaster = null, subDmgTarget = null;

            if (cfg.BreakOnControlCaster)
                subCtlCaster = ProcBus.SubscribeControlApply(a => { if (a.TgtSid == csidU) cancelRequested = true; });
            if (cfg.BreakOnControlTarget)
                subCtlTarget = ProcBus.SubscribeControlApply(a => { if (a.TgtSid == tsidU) cancelRequested = true; });

            if (cfg.BreakOnDamageCaster)
                subDmgCaster = ProcBus.SubscribeDamage(d => { if (d.TgtSid == csidU) cancelRequested = true; });
            if (cfg.BreakOnDamageTarget)
                subDmgTarget = ProcBus.SubscribeDamage(d => { if (d.TgtSid == tsidU) cancelRequested = true; });

            Func<bool> isCancelled = () =>
            {
                if (cancelRequested) return true;
                if (cfg.ExtraCancel != null && cfg.ExtraCancel()) return true;
                if (!rt.IsAlive(caster) || !rt.IsAlive(target)) return true;
                return false;
            };

            rt.StartChannel(
                csid, cfg.SpellId, MathF.Max(0.05f, cfg.Duration), MathF.Max(0.05f, cfg.TickEvery),
                isCancelled: isCancelled,
                onTick: () =>
                {
                    rt.ApplyShield(csid, tsid, cfg.SpellId, cfg.Tag, capPerTick, reapplyDur);
                    ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, csidU, tsidU, cfg.Tag, capPerTick, reapplyDur));
                },
                onEnd: () =>
                {
                    subCtlCaster?.Dispose();
                    subCtlTarget?.Dispose();
                    subDmgCaster?.Dispose();
                    subDmgTarget?.Dispose();
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // =========================
        //     ACCUMULATE CHANNEL
        // =========================
        public sealed class AccumulateConfig
        {
            public int   SpellId;
            public float TickValue;         // сколько прибавлять за тик
            public float Duration;
            public float TickEvery = 0.5f;
            public float Mana = 0;
            public float Gcd = 0;
            public float Cooldown = 0;

            public Func<bool>? ExtraCancel;
            public bool BreakOnControlCaster = true;
            public bool BreakOnControlTarget = false;
            public bool BreakOnDamageCaster  = false;
            public bool BreakOnDamageTarget  = false;

            /// Коллбек по завершению канала (если не отменён): передаём итоговую сумму
            public Action<ISpellRuntime, int /*csid*/, int /*tsid*/, float /*total*/>? OnComplete;
        }

        /// Канал, который копит значение и отдаёт его в конце (если не отменён).
        public static SpellResult Accumulate(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, AccumulateConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            float total = 0f;

            bool cancelRequested = false;
            IDisposable? subCtlCaster = null, subCtlTarget = null, subDmgCaster = null, subDmgTarget = null;

            ulong csidU = (ulong)csid;
            ulong tsidU = (ulong)tsid;

            if (cfg.BreakOnControlCaster)
                subCtlCaster = ProcBus.SubscribeControlApply(a => { if (a.TgtSid == csidU) cancelRequested = true; });
            if (cfg.BreakOnControlTarget)
                subCtlTarget = ProcBus.SubscribeControlApply(a => { if (a.TgtSid == tsidU) cancelRequested = true; });

            if (cfg.BreakOnDamageCaster)
                subDmgCaster = ProcBus.SubscribeDamage(d => { if (d.TgtSid == csidU) cancelRequested = true; });
            if (cfg.BreakOnDamageTarget)
                subDmgTarget = ProcBus.SubscribeDamage(d => { if (d.TgtSid == tsidU) cancelRequested = true; });

            Func<bool> isCancelled = () =>
            {
                if (cancelRequested) return true;
                if (cfg.ExtraCancel != null && cfg.ExtraCancel()) return true;
                if (!rt.IsAlive(caster) || !rt.IsAlive(target)) return true;
                return false;
            };

            rt.StartChannel(
                csid, cfg.SpellId, MathF.Max(0.05f, cfg.Duration), MathF.Max(0.05f, cfg.TickEvery),
                isCancelled: isCancelled,
                onTick: () =>
                {
                    total += MathF.Max(0, cfg.TickValue);
                    ProcBus.PublishPeriodicTick(new ProcBus.PeriodicTickArgs(cfg.SpellId, csidU, tsidU, cfg.TickValue, "channel:accumulate"));
                },
                onEnd: () =>
                {
                    subCtlCaster?.Dispose();
                    subCtlTarget?.Dispose();
                    subDmgCaster?.Dispose();
                    subDmgTarget?.Dispose();

                    if (!isCancelled() && cfg.OnComplete != null)
                        cfg.OnComplete(rt, csid, tsid, total);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}