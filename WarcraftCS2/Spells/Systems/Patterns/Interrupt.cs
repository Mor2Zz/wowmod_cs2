using System.Collections.Generic;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Срыв чужого каста/канала: удаляет соответствующие ауры по тегам (например, "channel", "casting").
    public static class Interrupt
    {
        public sealed class Config
        {
            public int    SpellId;
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            /// Какие теги считаем «кастом/каналом» и срываем.
            public List<string> Tags = new() { "channel", "casting" };

            public string? PlayFx;
            public string? PlaySfx;
        }

        /// Срывает (прерывает) канал/каст у цели, удаляя ауры с указанными тегами.
        public static SpellResult Apply(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana))
                return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (cfg.Tags != null && cfg.Tags.Count > 0)
            {
                foreach (var tag in cfg.Tags)
                    rt.RemoveAuraByTag(tsid, tag);
            }

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}