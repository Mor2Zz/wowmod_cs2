using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Вампиризм (лееч): урон + отхил на долю нанесённого урона.
    public static class Leech
    {
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // ---------------------------
        //  Instant Leech (удар-отхил)
        // ---------------------------
        public sealed class InstantConfig
        {
            public int    SpellId;
            public string School = "magic"; 
            public float  Damage;
            public float  LeechPercent01 = 0.3f; 

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult Instant(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, InstantConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) return SpellResult.Fail();

            var resist01 = Clamp01(rt.GetResist01(tsid, cfg.School));
            var dmg = MathF.Max(0, cfg.Damage * (1f - resist01));
            var heal = MathF.Max(0, dmg * Clamp01(cfg.LeechPercent01));

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (dmg > 0) rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
            if (heal > 0) rt.Heal(csid, csid, cfg.SpellId, heal);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            // события
            ulong csidU = (ulong)csid, tsidU = (ulong)tsid;
            if (dmg  > 0) ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, csidU, tsidU, dmg, cfg.School));
            if (heal > 0) ProcBus.PublishHeal  (new ProcBus.HealArgs  (cfg.SpellId, csidU, csidU, heal));

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------------------------
        //  DoT Leech (периодика)
        // ---------------------------
        public sealed class DotConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  TickDamage;
            public float  LeechPercent01 = 0.3f;
            public float  Duration;
            public float  TickEvery = 1.0f;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string AuraTag = "leech";
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult Dot(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, DotConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var dur  = MathF.Max(0.05f, cfg.Duration);
            var tick = MathF.Max(0.05f, cfg.TickEvery);

            // пометим ауру на цели (для UI/диспела)
            rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.AuraTag, cfg.TickDamage, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            ulong csidU = (ulong)csid, tsidU = (ulong)tsid;

            rt.StartPeriodic(
                csid, tsid, cfg.SpellId,
                dur, tick,
                onTick: () =>
                {
                    if (!rt.IsAlive(target) || !rt.IsAlive(caster)) return;

                    var resist01 = Clamp01(rt.GetResist01(tsid, cfg.School));
                    var dmg  = MathF.Max(0, cfg.TickDamage * (1f - resist01));
                    if (dmg <= 0f) return;

                    var heal = MathF.Max(0, dmg * Clamp01(cfg.LeechPercent01));

                    rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                    if (heal > 0) rt.Heal(csid, csid, cfg.SpellId, heal);

                    ProcBus.PublishPeriodicTick(new ProcBus.PeriodicTickArgs(cfg.SpellId, csidU, tsidU, dmg, "leech:dot"));
                    ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, csidU, tsidU, dmg, cfg.School));
                    if (heal > 0) ProcBus.PublishHeal(new ProcBus.HealArgs(cfg.SpellId, csidU, csidU, heal));
                },
                onEnd: () => rt.RemoveAuraByTag(tsid, cfg.AuraTag)
            );

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}
