using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Area;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    public static class Mass
    {
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // ---------------- Allies: Sphere buff ----------------
        public sealed class AlliesSphereAuraConfig
        {
            public int    SpellId;
            public string Tag = "aura";
            public float  Magnitude = 1f;
            public float  Radius = 8f;
            public float  Duration = 5f;
            public float  Mana = 0;
            public float  Gcd  = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult AlliesSphereAura(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            AlliesSphereAuraConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var allies = AreaCollectors.Sphere(caster, candidates, cfg.Radius, t => rt.IsAlly(caster, t));
            if (allies.Count == 0) return SpellResult.Fail();

            float dur = MathF.Max(0.05f, cfg.Duration);
            foreach (var a in allies)
                rt.ApplyAura(csid, rt.SidOf(a), cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------------- Enemies: Sphere debuff (LoS) ----------------
        public sealed class EnemiesSphereAuraConfig
        {
            public bool   RequireLoS = false;
            public bool   WorldOnly  = false;

            public int    SpellId;
            public string Tag = "debuff";
            public float  Magnitude = 1f;
            public float  Radius = 8f;
            public float  Duration = 5f;
            public float  Mana = 0;
            public float  Gcd  = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult EnemiesSphereAura(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            EnemiesSphereAuraConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var enemies = AreaCollectors.Sphere(caster, candidates, cfg.Radius, t => rt.IsEnemy(caster, t));
            if (cfg.RequireLoS)
            {
                var filtered = new List<TargetSnapshot>(enemies.Count);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var t = enemies[i];
                    bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                    if (ok) filtered.Add(t);
                }
                enemies = filtered;
            }
            if (enemies.Count == 0) return SpellResult.Fail();

            float dur = MathF.Max(0.05f, cfg.Duration);
            foreach (var e in enemies)
                rt.ApplyAura(csid, rt.SidOf(e), cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------------- Enemies: Cone debuff (LoS) ----------------
        public sealed class EnemiesConeAuraConfig
        {
            public bool   RequireLoS = false;
            public bool   WorldOnly  = false;

            public int    SpellId;
            public string Tag = "debuff";
            public float  Magnitude = 1f;
            public float  Range = 10f;
            public float  HalfAngleDeg = 30f;
            public float  Duration = 5f;
            public float  Mana = 0;
            public float  Gcd  = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult EnemiesConeAura(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            EnemiesConeAuraConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var enemies = AreaCollectors.Cone(caster, candidates, cfg.Range, cfg.HalfAngleDeg, t => rt.IsEnemy(caster, t));
            if (cfg.RequireLoS)
            {
                var filtered = new List<TargetSnapshot>(enemies.Count);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var t = enemies[i];
                    bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                    if (ok) filtered.Add(t);
                }
                enemies = filtered;
            }
            if (enemies.Count == 0) return SpellResult.Fail();

            float dur = MathF.Max(0.05f, cfg.Duration);
            foreach (var e in enemies)
                rt.ApplyAura(csid, rt.SidOf(e), cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------------- Dispel (through runtime categories) ----------------
        // Allies sphere dispel
        public sealed class AlliesSphereDispelConfig
        {
            public int   SpellId;
            public float Radius = 8f;
            public int   MaxPerTick = 6;
            public List<string> Categories = new() { "magic" }; // можно добавить "curse","poison","disease"
            public float Mana = 0;
            public float Gcd  = 0;
            public float Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult AlliesSphereDispel(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            AlliesSphereDispelConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var allies = AreaCollectors.Sphere(caster, candidates, cfg.Radius, t => rt.IsAlly(caster, t));
            if (allies.Count == 0) return SpellResult.Fail();

            int removedAny = 0;
            int hits = 0;
            for (int i = 0; i < allies.Count && hits < cfg.MaxPerTick; i++)
            {
                var tsid = rt.SidOf(allies[i]);
                bool removedThis = false;
                for (int c = 0; c < cfg.Categories.Count && hits < cfg.MaxPerTick; c++)
                {
                    var cat = cfg.Categories[c];
                    var removed = rt.DispelByCategory(tsid, cat, 1);
                    if (removed > 0) { removedAny += removed; hits += removed; removedThis = true; }
                }
                if (removedThis && hits >= cfg.MaxPerTick) break;
            }

            if (removedAny <= 0) return SpellResult.Fail();

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // Enemies sphere dispel (LoS)
        public sealed class EnemiesSphereDispelConfig
        {
            public bool   RequireLoS = false;
            public bool   WorldOnly  = false;

            public int   SpellId;
            public float Radius = 8f;
            public int   MaxPerTick = 6;
            public List<string> Categories = new() { "magic" }; // бафы врагов по категориям
            public float Mana = 0;
            public float Gcd  = 0;
            public float Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult EnemiesSphereDispel(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            EnemiesSphereDispelConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var enemies = AreaCollectors.Sphere(caster, candidates, cfg.Radius, t => rt.IsEnemy(caster, t));
            if (cfg.RequireLoS)
            {
                var filtered = new List<TargetSnapshot>(enemies.Count);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var t = enemies[i];
                    bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                    if (ok) filtered.Add(t);
                }
                enemies = filtered;
            }
            if (enemies.Count == 0) return SpellResult.Fail();

            int removedAny = 0;
            int hits = 0;
            for (int i = 0; i < enemies.Count && hits < cfg.MaxPerTick; i++)
            {
                var tsid = rt.SidOf(enemies[i]);
                bool removedThis = false;
                for (int c = 0; c < cfg.Categories.Count && hits < cfg.MaxPerTick; c++)
                {
                    var cat = cfg.Categories[c];
                    var removed = rt.DispelByCategory(tsid, cat, 1);
                    if (removed > 0) { removedAny += removed; hits += removed; removedThis = true; }
                }
                if (removedThis && hits >= cfg.MaxPerTick) break;
            }

            if (removedAny <= 0) return SpellResult.Fail();

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // Allies cone dispel
        public sealed class AlliesConeDispelConfig
        {
            public int   SpellId;
            public float Range = 10f;
            public float HalfAngleDeg = 30f;
            public int   MaxPerTick = 6;
            public List<string> Categories = new() { "magic" };
            public float Mana = 0;
            public float Gcd  = 0;
            public float Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult AlliesConeDispel(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            AlliesConeDispelConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var allies = AreaCollectors.Cone(caster, candidates, cfg.Range, cfg.HalfAngleDeg, t => rt.IsAlly(caster, t));
            if (allies.Count == 0) return SpellResult.Fail();

            int removedAny = 0;
            int hits = 0;
            for (int i = 0; i < allies.Count && hits < cfg.MaxPerTick; i++)
            {
                var tsid = rt.SidOf(allies[i]);
                bool removedThis = false;
                for (int c = 0; c < cfg.Categories.Count && hits < cfg.MaxPerTick; c++)
                {
                    var cat = cfg.Categories[c];
                    var removed = rt.DispelByCategory(tsid, cat, 1);
                    if (removed > 0) { removedAny += removed; hits += removed; removedThis = true; }
                }
                if (removedThis && hits >= cfg.MaxPerTick) break;
            }

            if (removedAny <= 0) return SpellResult.Fail();

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // Enemies cone dispel (LoS)
        public sealed class EnemiesConeDispelConfig
        {
            public bool   RequireLoS = false;
            public bool   WorldOnly  = false;

            public int   SpellId;
            public float Range = 10f;
            public float HalfAngleDeg = 30f;
            public int   MaxPerTick = 6;
            public List<string> Categories = new() { "magic" };
            public float Mana = 0;
            public float Gcd  = 0;
            public float Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult EnemiesConeDispel(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            EnemiesConeDispelConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var enemies = AreaCollectors.Cone(caster, candidates, cfg.Range, cfg.HalfAngleDeg, t => rt.IsEnemy(caster, t));
            if (cfg.RequireLoS)
            {
                var filtered = new List<TargetSnapshot>(enemies.Count);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var t = enemies[i];
                    bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                    if (ok) filtered.Add(t);
                }
                enemies = filtered;
            }
            if (enemies.Count == 0) return SpellResult.Fail();

            int removedAny = 0;
            int hits = 0;
            for (int i = 0; i < enemies.Count && hits < cfg.MaxPerTick; i++)
            {
                var tsid = rt.SidOf(enemies[i]);
                bool removedThis = false;
                for (int c = 0; c < cfg.Categories.Count && hits < cfg.MaxPerTick; c++)
                {
                    var cat = cfg.Categories[c];
                    var removed = rt.DispelByCategory(tsid, cat, 1);
                    if (removed > 0) { removedAny += removed; hits += removed; removedThis = true; }
                }
                if (removedThis && hits >= cfg.MaxPerTick) break;
            }

            if (removedAny <= 0) return SpellResult.Fail();

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------------- Control enemies (LoS) ----------------
        public sealed class EnemiesSphereControlConfig
        {
            public bool   RequireLoS = false;
            public bool   WorldOnly  = false;

            public int   SpellId;
            public float Radius = 8f;
            public string Tag = "stun";
            public float Magnitude = 1f;
            public float Duration  = 2f;
            public float Mana = 0;
            public float Gcd  = 0;
            public float Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult EnemiesSphereControl(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            EnemiesSphereControlConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var enemies = AreaCollectors.Sphere(caster, candidates, cfg.Radius, t => rt.IsEnemy(caster, t));
            if (cfg.RequireLoS)
            {
                var filtered = new List<TargetSnapshot>(enemies.Count);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var t = enemies[i];
                    bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                    if (ok) filtered.Add(t);
                }
                enemies = filtered;
            }
            if (enemies.Count == 0) return SpellResult.Fail();

            float dur = MathF.Max(0.05f, cfg.Duration);
            foreach (var e in enemies)
                rt.ApplyAura(csid, rt.SidOf(e), cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public sealed class EnemiesConeControlConfig
        {
            public bool   RequireLoS = false;
            public bool   WorldOnly  = false;

            public int   SpellId;
            public float Range = 10f;
            public float HalfAngleDeg = 30f;
            public string Tag = "stun";
            public float Magnitude = 1f;
            public float Duration  = 2f;
            public float Mana = 0;
            public float Gcd  = 0;
            public float Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult EnemiesConeControl(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            EnemiesConeControlConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var enemies = AreaCollectors.Cone(caster, candidates, cfg.Range, cfg.HalfAngleDeg, t => rt.IsEnemy(caster, t));
            if (cfg.RequireLoS)
            {
                var filtered = new List<TargetSnapshot>(enemies.Count);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var t = enemies[i];
                    bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                    if (ok) filtered.Add(t);
                }
                enemies = filtered;
            }
            if (enemies.Count == 0) return SpellResult.Fail();

            float dur = MathF.Max(0.05f, cfg.Duration);
            foreach (var e in enemies)
                rt.ApplyAura(csid, rt.SidOf(e), cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------------- Damage enemies (LoS) ----------------
        public sealed class EnemiesSphereDamageConfig
        {
            public bool   RequireLoS = false;
            public bool   WorldOnly  = false;

            public int   SpellId;
            public float Radius;
            public string School = "magic";
            public float  Amount;
            public string? RequireNoImmunityTag;
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult EnemiesSphereDamage(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            EnemiesSphereDamageConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var enemies = AreaCollectors.Sphere(caster, candidates, cfg.Radius, t => rt.IsEnemy(caster, t));
            if (cfg.RequireLoS)
            {
                var filtered = new List<TargetSnapshot>(enemies.Count);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var t = enemies[i];
                    bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                    if (ok) filtered.Add(t);
                }
                enemies = filtered;
            }
            if (enemies.Count == 0) return SpellResult.Fail();

            int affected = 0;
            foreach (var e in enemies)
            {
                var tsid = rt.SidOf(e);
                if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;
                if (cfg.RequireNoImmunityTag != null && rt.HasImmunity(tsid, cfg.RequireNoImmunityTag)) continue;

                float resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                float dmg = MathF.Max(0f, cfg.Amount * (1f - resist));
                if (dmg <= 0f) continue;

                rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                affected++;
            }
            if (affected <= 0) return SpellResult.Fail();

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public sealed class EnemiesConeDamageConfig
        {
            public bool   RequireLoS = false;
            public bool   WorldOnly  = false;

            public int   SpellId;
            public string School = "magic";
            public float Range;
            public float HalfAngleDeg = 30f;
            public float  Amount;
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult EnemiesConeDamage(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            EnemiesConeDamageConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var enemies = AreaCollectors.Cone(caster, candidates, cfg.Range, cfg.HalfAngleDeg, t => rt.IsEnemy(caster, t));
            if (cfg.RequireLoS)
            {
                var filtered = new List<TargetSnapshot>(enemies.Count);
                for (int i = 0; i < enemies.Count; i++)
                {
                    var t = enemies[i];
                    bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                    if (ok) filtered.Add(t);
                }
                enemies = filtered;
            }
            if (enemies.Count == 0) return SpellResult.Fail();

            int affected = 0;
            foreach (var e in enemies)
            {
                var tsid = rt.SidOf(e);
                if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;

                float resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                float dmg = MathF.Max(0f, cfg.Amount * (1f - resist));
                if (dmg <= 0f) continue;

                rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                affected++;
            }
            if (affected <= 0) return SpellResult.Fail();

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------------- Healing allies ----------------
        public sealed class AlliesSphereHealConfig
        {
            public int   SpellId;
            public float Radius;
            public float Amount;
            public float Mana = 0;
            public float Gcd  = 0;
            public float Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult AlliesSphereHeal(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            AlliesSphereHealConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var allies = AreaCollectors.Sphere(caster, candidates, cfg.Radius, t => rt.IsAlly(caster, t));
            if (allies.Count == 0) return SpellResult.Fail();

            int healed = 0;
            foreach (var a in allies)
            {
                rt.Heal(csid, rt.SidOf(a), cfg.SpellId, MathF.Max(0f, cfg.Amount));
                healed++;
            }
            if (healed <= 0) return SpellResult.Fail();

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public sealed class AlliesConeHealConfig
        {
            public int   SpellId;
            public float Range;
            public float HalfAngleDeg = 30f;
            public float Amount;
            public float Mana = 0;
            public float Gcd  = 0;
            public float Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult AlliesConeHeal(
            ISpellRuntime rt,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            AlliesConeHealConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var allies = AreaCollectors.Cone(caster, candidates, cfg.Range, cfg.HalfAngleDeg, t => rt.IsAlly(caster, t));
            if (allies.Count == 0) return SpellResult.Fail();

            int healed = 0;
            foreach (var a in allies)
            {
                rt.Heal(csid, rt.SidOf(a), cfg.SpellId, MathF.Max(0f, cfg.Amount));
                healed++;
            }
            if (healed <= 0) return SpellResult.Fail();

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}