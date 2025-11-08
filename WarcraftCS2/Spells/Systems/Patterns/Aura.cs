using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Универсальные обёртки для аур: баф/дебаф.
    public static class Aura
    {
        public sealed class ApplyConfig
        {
            public int    SpellId;
            public string Tag = "aura";
            public float  Magnitude = 1f;
            public float  Duration  = 5f;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFx;  public string? PlaySfx;
        }

        public static SpellResult ApplyBuff(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, ApplyConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            int csid = rt.SidOf(caster), tsid = rt.SidOf(target);
            if (!rt.IsAlly(caster, target)) return SpellResult.Fail();

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            float dur = MathF.Max(0.05f, cfg.Duration);
            rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);
            ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, cfg.Tag, cfg.Magnitude, dur));

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public static SpellResult ApplyDebuff(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, ApplyConfig cfg, string schoolForImmuneCheck = "magic")
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            int csid = rt.SidOf(caster), tsid = rt.SidOf(target);
            if (!rt.IsEnemy(caster, target)) return SpellResult.Fail();
            if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, schoolForImmuneCheck)) return SpellResult.Fail();

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            float dur = MathF.Max(0.05f, cfg.Duration);
            rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);
            ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, cfg.Tag, cfg.Magnitude, dur));

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public static void RemoveByTag(ISpellRuntime rt, TargetSnapshot target, string tag)
        {
            int tsid = rt.SidOf(target);
            rt.RemoveAuraByTag(tsid, tag);
        }
    }
}