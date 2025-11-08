using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    public static class Spellsteal
    {
        public sealed class Config
        {
            public bool RequireLoSFromCaster = true;
            public bool WorldOnly = false;

            public int SpellId;

            public List<string> Tags = new();
            public int   MaxTags = 0;
            public float ApplyDuration = 8f;
            public float ApplyValue = 1f;

            public List<string> Categories = new();
            public int   MaxByCategory = 1;
            public string StolenTag = "stolen";

            public float Mana = 0;
            public float Gcd = 0;
            public float Cooldown = 0;

            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult Apply(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.RequireLoSFromCaster)
            {
                bool visible = cfg.WorldOnly
                    ? LineOfSight.VisibleWorldOnly(caster, target)
                    : LineOfSight.Visible(caster, target);
                if (!visible) return SpellResult.Fail();
            }

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            int totalStolen = 0;

            // 1) Кража по тегам
            if (cfg.Tags != null && cfg.Tags.Count > 0)
            {
                int processed = 0;
                foreach (var tag in cfg.Tags)
                {
                    if (cfg.MaxTags > 0 && processed >= cfg.MaxTags) break;
                    if (string.IsNullOrWhiteSpace(tag)) continue;

                    if (TransferTag(rt, csid, tsid, cfg.SpellId, tag, cfg.ApplyValue, MathF.Max(0.05f, cfg.ApplyDuration)))
                    {
                        totalStolen++;
                        processed++;
                    }
                }
            }

            // 2) Кража по категориям
            if (cfg.Categories != null && cfg.Categories.Count > 0 && (cfg.MaxByCategory != 0))
            {
                int left = cfg.MaxByCategory <= 0 ? int.MaxValue : cfg.MaxByCategory;
                for (int i = 0; i < cfg.Categories.Count && left > 0; i++)
                {
                    var cat = cfg.Categories[i];
                    if (string.IsNullOrWhiteSpace(cat)) continue;

                    var removed = rt.DispelByCategory(tsid, cat, left);
                    if (removed > 0)
                    {
                        totalStolen += removed;
                        left -= removed;

                        float dur = MathF.Max(0.05f, cfg.ApplyDuration);
                        rt.ApplyAura(csid, csid, cfg.SpellId, cfg.StolenTag, removed, dur);
                        ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)csid, cfg.StolenTag, removed, dur));
                    }
                }
            }

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            return totalStolen > 0 ? SpellResult.Ok(cfg.Mana, cfg.Cooldown) : SpellResult.Fail();
        }

        private static bool TransferTag(ISpellRuntime rt, int casterSid, int targetSid, int spellId, string tag, float value, float duration)
        {
            rt.RemoveAuraByTag(targetSid, tag);
            float dur = MathF.Max(0.05f, duration);
            rt.ApplyAura(casterSid, casterSid, spellId, tag, value, dur);
            ProcBus.PublishAuraApply(new ProcBus.AuraArgs(spellId, (ulong)casterSid, (ulong)casterSid, tag, value, dur));
            return true;
        }
    }
}