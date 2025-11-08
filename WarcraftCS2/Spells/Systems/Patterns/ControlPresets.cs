using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Пресеты поверх Control: Silence / Interrupt (через ApplyControlWithDr + опц. снятие тегов).
    public static class ControlPresets
    {
        public sealed class SilenceConfig
        {
            public int   SpellId;
            public string Tag = "silence";
            public float Duration = 2f;

            public float Mana = 0f;
            public float Gcd  = 0f;
            public float Cooldown = 0f;
            public bool  BreakOnDamage = false;
            public float BreakFlat = 0f;
            public float BreakPercent01 = 0f;
        }

        public static SpellResult Silence(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, SilenceConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();
            int csid = rt.SidOf(caster), tsid = rt.SidOf(target);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana > 0f)     rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd > 0f)      rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            float applied = rt.ApplyControlWithDr(csid, tsid, cfg.SpellId, cfg.Tag, MathF.Max(0.05f, cfg.Duration));
            return applied > 0f ? SpellResult.Ok(cfg.Mana, cfg.Cooldown) : SpellResult.Fail();
        }

        public sealed class InterruptConfig
        {
            public int   SpellId;
            public string Tag = "interrupt";     // короткий «глушняк» для сбития каста
            public float Duration = 0.2f;

            public string[] CancelTags = Array.Empty<string>(); // какие теги снять у цели (например, "channel")
            public float Mana = 0f;
            public float Gcd  = 0f;
            public float Cooldown = 0f;
        }

        public static SpellResult Interrupt(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, InterruptConfig cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();
            int csid = rt.SidOf(caster), tsid = rt.SidOf(target);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana > 0f)     rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd > 0f)      rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            // короткий контроль через DR
            rt.ApplyControlWithDr(csid, tsid, cfg.SpellId, cfg.Tag, MathF.Max(0.05f, cfg.Duration));

            // снимаем канал/каст-теги, если указаны
            for (int i = 0; i < cfg.CancelTags.Length; i++)
            {
                var tag = cfg.CancelTags[i];
                if (!string.IsNullOrWhiteSpace(tag))
                    rt.RemoveAuraByTag(tsid, tag);
            }

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}
