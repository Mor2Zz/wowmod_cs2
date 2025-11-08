using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    public static class Dispel
    {
        // ---------- Single target: категории ----------
        public sealed class Config
        {
            public int   SpellId;
            public string Category = "magic"; // "magic","curse","poison","disease","enrage", ...
            public int   MaxCount = 1;

            public float Mana = 0;
            public float Gcd  = 0;
            public float Cooldown = 0;
        }

        public static SpellResult Apply(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana))
                return SpellResult.Fail();

            if (cfg.Mana > 0)     rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd > 0)      rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var removed = rt.DispelByCategory(tsid, cfg.Category, cfg.MaxCount);
            return removed > 0 ? SpellResult.Ok(cfg.Mana, cfg.Cooldown) : SpellResult.Fail();
        }

        // ---------- Single target: теги ----------
        public sealed class TagsConfig
        {
            public int    SpellId;
            public string[] Tags = Array.Empty<string>();

            public float Mana = 0;
            public float Gcd  = 0;
            public float Cooldown = 0;
        }

        public static SpellResult RemoveTags(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, TagsConfig cfg)
        {
            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana))
                return SpellResult.Fail();

            if (cfg.Mana > 0)     rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd > 0)      rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            bool removedAny = false;
            for (int i = 0; i < cfg.Tags.Length; i++)
            {
                var tag = cfg.Tags[i];
                if (string.IsNullOrWhiteSpace(tag)) continue;
                rt.RemoveAuraByTag(tsid, tag);
                removedAny = true; // RemoveAuraByTag не возвращает статус — считаем успешным действием
            }

            return removedAny ? SpellResult.Ok(cfg.Mana, cfg.Cooldown) : SpellResult.Fail();
        }

        // ---------- Area: категории ----------
        public enum Filter { All, Allies, Enemies }

        public sealed class AreaConfig
        {
            public int    SpellId;
            public string Category = "magic";
            public int    MaxCountPerTarget = 1;

            public float  Radius = 6f;
            public int    MaxTargets = 8;
            public bool   Flat = true;
            public Filter TargetFilter = Filter.All;

            // LoS от кастера к цели (по умолчанию выключен)
            public bool   RequireLoSFromCaster = false;
            public bool   WorldOnly = false;

            public float  Mana = 0;
            public float  Gcd  = 0;
            public float  Cooldown = 0;
        }

        public static SpellResult MassByCategory(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot center,
            IReadOnlyList<TargetSnapshot> candidates,
            AreaConfig cfg)
        {
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana))
                return SpellResult.Fail();

            var cpos = center.Position;
            float r2 = cfg.Radius * cfg.Radius;

            int hits = 0;
            int removedAny = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                if (!rt.IsAlive(t)) continue;

                if (cfg.TargetFilter == Filter.Allies  && !rt.IsAlly(caster, t))  continue;
                if (cfg.TargetFilter == Filter.Enemies && !rt.IsEnemy(caster, t)) continue;

                var p = t.Position;
                float dx = p.X - cpos.X, dy = p.Y - cpos.Y, dz = p.Z - cpos.Z;
                if (cfg.Flat) dz = 0f;
                float d2 = dx * dx + dy * dy + dz * dz;
                if (d2 > r2) continue;

                if (cfg.RequireLoSFromCaster)
                {
                    bool ok = cfg.WorldOnly
                        ? LineOfSight.VisibleWorldOnly(caster, t)
                        : LineOfSight.Visible(caster, t);
                    if (!ok) continue;
                }

                int tsid = rt.SidOf(t);
                var removed = rt.DispelByCategory(tsid, cfg.Category, cfg.MaxCountPerTarget);
                if (removed > 0)
                {
                    removedAny += removed;
                    hits++;
                    if (hits >= cfg.MaxTargets) break;
                }
            }

            if (removedAny <= 0) return SpellResult.Fail();

            if (cfg.Mana > 0)     rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd > 0)      rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------- Area: теги ----------
        public sealed class AreaTagsConfig
        {
            public int    SpellId;
            public string[] Tags = Array.Empty<string>();

            public float  Radius = 6f;
            public int    MaxTargets = 8;
            public bool   Flat = true;
            public Filter TargetFilter = Filter.All;

            public bool   RequireLoSFromCaster = false;
            public bool   WorldOnly = false;

            public float  Mana = 0;
            public float  Gcd  = 0;
            public float  Cooldown = 0;
        }

        public static SpellResult RemoveTagsArea(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot center,
            IReadOnlyList<TargetSnapshot> candidates,
            AreaTagsConfig cfg)
        {
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana))
                return SpellResult.Fail();

            var cpos = center.Position;
            float r2 = cfg.Radius * cfg.Radius;

            int hits = 0;
            int removedAny = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                if (!rt.IsAlive(t)) continue;

                if (cfg.TargetFilter == Filter.Allies  && !rt.IsAlly(caster, t))  continue;
                if (cfg.TargetFilter == Filter.Enemies && !rt.IsEnemy(caster, t)) continue;

                var p = t.Position;
                float dx = p.X - cpos.X, dy = p.Y - cpos.Y, dz = p.Z - cpos.Z;
                if (cfg.Flat) dz = 0f;
                float d2 = dx * dx + dy * dy + dz * dz;
                if (d2 > r2) continue;

                if (cfg.RequireLoSFromCaster)
                {
                    bool ok = cfg.WorldOnly
                        ? LineOfSight.VisibleWorldOnly(caster, t)
                        : LineOfSight.Visible(caster, t);
                    if (!ok) continue;
                }

                int tsid = rt.SidOf(t);
                bool removedThis = false;

                for (int j = 0; j < cfg.Tags.Length; j++)
                {
                    var tag = cfg.Tags[j];
                    if (string.IsNullOrWhiteSpace(tag)) continue;
                    rt.RemoveAuraByTag(tsid, tag);
                    removedThis = true;
                }

                if (removedThis)
                {
                    removedAny++;
                    hits++;
                    if (hits >= cfg.MaxTargets) break;
                }
            }

            if (removedAny <= 0) return SpellResult.Fail();

            if (cfg.Mana > 0)     rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd > 0)      rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}