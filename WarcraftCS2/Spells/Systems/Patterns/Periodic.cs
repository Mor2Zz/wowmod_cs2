using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// <summary>Периодические эффекты: DoT/HoT.</summary>
    public static class Periodic
    {
        // --- helpers ---
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // ---------------------------
        // DoT (damage over time)
        // ---------------------------
        public sealed class DotConfig
        {
            public int    SpellId;
            public string School = "magic";   // "physical","holy","fire","frost","nature","shadow","arcane", ...
            public float  TickDamage;
            public float  Duration;
            public float  TickEvery = 1.0f;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string AuraTag = "dot";
            public string? PlayFx;  public string? PlaySfx;
        }

        /// <summary>Вешает DoT на цель, тикает уроном каждые TickEvery секунд.</summary>
        public static SpellResult ApplyDot(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, DotConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csidInt = rt.SidOf(caster);
            var tsidInt = rt.SidOf(target);
            ulong csid = (ulong)csidInt;       // для ProcBus
            ulong tsid = (ulong)tsidInt;

            if (cfg.Mana > 0 && !rt.HasMana(csidInt, cfg.Mana)) return SpellResult.Fail();
            if (rt.HasImmunity(tsidInt, "all") || rt.HasImmunity(tsidInt, cfg.School)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csidInt, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csidInt, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csidInt, cfg.SpellId, cfg.Cooldown);

            var dur  = MathF.Max(0.05f, cfg.Duration);
            var tick = MathF.Max(0.05f, cfg.TickEvery);

            // помечаем ауру для UI/диспела
            rt.ApplyAura(csidInt, tsidInt, cfg.SpellId, cfg.AuraTag, cfg.TickDamage, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            // периодик
            var handle = rt.StartPeriodic(
                csidInt, tsidInt, cfg.SpellId,
                dur, tick,
                onTick: () =>
                {
                    if (!rt.IsAlive(target)) return;

                    var resist01 = Clamp01(rt.GetResist01(tsidInt, cfg.School));
                    var dmg = MathF.Max(0, cfg.TickDamage * (1f - resist01));
                    if (dmg <= 0f) return;

                    rt.DealDamage(csidInt, tsidInt, cfg.SpellId, dmg, cfg.School);

                    // события для талантов/аур
                    ProcBus.PublishPeriodicTick(new ProcBus.PeriodicTickArgs(cfg.SpellId, csid, tsid, dmg, "dot"));
                    ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, csid, tsid, dmg, cfg.School));
                },
                onEnd: () => rt.RemoveAuraByTag(tsidInt, cfg.AuraTag)
            );

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------------------------
        // HoT (heal over time)
        // ---------------------------
        public sealed class HotConfig
        {
            public int    SpellId;
            public float  TickHeal;
            public float  Duration;
            public float  TickEvery = 1.0f;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string AuraTag = "hot";
            public string? PlayFx;  public string? PlaySfx;
        }

        /// <summary>Вешает HoT на цель, тикает отхилом каждые TickEvery секунд.</summary>
        public static SpellResult ApplyHot(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, HotConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csidInt = rt.SidOf(caster);
            var tsidInt = rt.SidOf(target);
            ulong csid = (ulong)csidInt;       // для ProcBus
            ulong tsid = (ulong)tsidInt;

            if (cfg.Mana > 0 && !rt.HasMana(csidInt, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csidInt, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csidInt, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csidInt, cfg.SpellId, cfg.Cooldown);

            var dur  = MathF.Max(0.05f, cfg.Duration);
            var tick = MathF.Max(0.05f, cfg.TickEvery);

            rt.ApplyAura(csidInt, tsidInt, cfg.SpellId, cfg.AuraTag, cfg.TickHeal, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            var handle = rt.StartPeriodic(
                csidInt, tsidInt, cfg.SpellId,
                dur, tick,
                onTick: () =>
                {
                    if (!rt.IsAlive(target)) return;

                    var heal = MathF.Max(0, cfg.TickHeal);
                    if (heal <= 0f) return;

                    rt.Heal(csidInt, tsidInt, cfg.SpellId, heal);

                    // события для талантов/аур
                    ProcBus.PublishPeriodicTick(new ProcBus.PeriodicTickArgs(cfg.SpellId, csid, tsid, heal, "hot"));
                    ProcBus.PublishHeal(new ProcBus.HealArgs(cfg.SpellId, csid, tsid, heal));
                },
                onEnd: () => rt.RemoveAuraByTag(tsidInt, cfg.AuraTag)
            );

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}