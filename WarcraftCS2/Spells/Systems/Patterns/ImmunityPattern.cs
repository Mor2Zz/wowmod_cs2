using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Иммунитеты по школам через ауры: immune:<school> или immune:all
    public static class ImmunityPattern
    {
        public sealed class Config
        {
            public int    SpellId;
            /// "all" или одна из школ: "physical","holy","fire","frost","nature","shadow","arcane"
            public string School = "all";
            public float  Duration = 3f;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            /// Тег ауры будет вида $"{TagPrefix}:{School}"
            public string TagPrefix = "immune";

            public string? PlayFx;
            public string? PlaySfx;
        }

        private static string MakeTag(string prefix, string school)
        {
            var s = string.IsNullOrWhiteSpace(school) ? "all" : school.ToLowerInvariant();
            return $"{prefix}:{s}";
        }

        private static SpellResult ApplyCommon(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            int csid = rt.SidOf(caster);
            int tsid = rt.SidOf(target);

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana))
                return SpellResult.Fail();

            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            float dur = MathF.Max(0.05f, cfg.Duration);
            string tag = MakeTag(cfg.TagPrefix, cfg.School);

            rt.ApplyAura(csid, tsid, cfg.SpellId, tag, 1f, dur);
            ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, tag, 1f, dur));

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        /// Иммунитет союзнику (провалится, если target не союзник кастера).
        public static SpellResult ApplyToAlly(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlly(caster, target)) return SpellResult.Fail();
            return ApplyCommon(rt, caster, target, cfg);
        }

        /// Иммунитет врагу (провалится, если target не враг кастера).
        public static SpellResult ApplyToEnemy(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsEnemy(caster, target)) return SpellResult.Fail();
            return ApplyCommon(rt, caster, target, cfg);
        }

        /// Иммунитет самому себе (кастеру).
        public static SpellResult ApplySelf(ISpellRuntime rt, TargetSnapshot caster, Config cfg)
        {
            return ApplyCommon(rt, caster, caster, cfg);
        }

        /// Снять конкретный школьный иммунитет с цели.
        public static void Remove(ISpellRuntime rt, TargetSnapshot target, string school, string tagPrefix = "immune")
        {
            int tsid = rt.SidOf(target);
            rt.RemoveAuraByTag(tsid, MakeTag(tagPrefix, school));
        }

        /// Снять все иммунитеты (all + по школам) с цели.
        public static void RemoveAll(ISpellRuntime rt, TargetSnapshot target, string tagPrefix = "immune")
        {
            int tsid = rt.SidOf(target);
            rt.RemoveAuraByTag(tsid, MakeTag(tagPrefix, "all"));
            rt.RemoveAuraByTag(tsid, MakeTag(tagPrefix, "physical"));
            rt.RemoveAuraByTag(tsid, MakeTag(tagPrefix, "holy"));
            rt.RemoveAuraByTag(tsid, MakeTag(tagPrefix, "fire"));
            rt.RemoveAuraByTag(tsid, MakeTag(tagPrefix, "frost"));
            rt.RemoveAuraByTag(tsid, MakeTag(tagPrefix, "nature"));
            rt.RemoveAuraByTag(tsid, MakeTag(tagPrefix, "shadow"));
            rt.RemoveAuraByTag(tsid, MakeTag(tagPrefix, "arcane"));
        }
    }
}