using System;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Мгновенные эффекты: прямой урон / хил / щит.
    public static class Instant
    {
        public sealed class DamageConfig
        {
            public bool   RequireLoSFromCaster = true; // было false
            public bool   WorldOnly = false;

            public int    SpellId;
            public string School = "magic"; // "physical","holy","fire","frost","nature","shadow","arcane"
            public float  Amount;
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult DirectDamage(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, DamageConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            int csid = rt.SidOf(caster);
            int tsid = rt.SidOf(target);

            if (cfg.RequireLoSFromCaster)
            {
                bool visible = cfg.WorldOnly
                    ? LineOfSight.VisibleWorldOnly(caster, target)
                    : LineOfSight.Visible(caster, target);
                if (!visible) return SpellResult.Fail();
            }

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (rt.HasImmunity(tsid, cfg.School) || rt.HasImmunity(tsid, "all")) return SpellResult.Fail();

            float resist = rt.GetResist01(tsid, cfg.School);
            float dmg = MathF.Max(0, cfg.Amount * (1f - resist));

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);

            if (dmg > 0)
                ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, cfg.School, critical:false));

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public sealed class HealConfig
        {
            public int    SpellId;
            public float  Amount;
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult DirectHeal(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, HealConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            int csid = rt.SidOf(caster);
            int tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            rt.Heal(csid, tsid, cfg.SpellId, MathF.Max(0, cfg.Amount));

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public sealed class ShieldConfig
        {
            public int    SpellId;
            public string Tag = "shield";
            public float  Capacity;
            public float  Duration = 5f;
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult Shield(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, ShieldConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            int csid = rt.SidOf(caster);
            int tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            float cap = MathF.Max(1, cfg.Capacity);
            float dur = MathF.Max(0.05f, cfg.Duration);

            rt.ApplyShield(csid, tsid, cfg.SpellId, cfg.Tag, cap, dur);

            ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, cfg.Tag, cap, dur));

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}