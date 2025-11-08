using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Area;
using WarcraftCS2.Spells.Systems.Core.LineOfSight;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// AoE-паттерны: конус и цепочка (chain).
    public static class AoE
    {
        public enum ShareMode { Uniform, RadialLinear }

        public sealed class ConeDamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  TotalDamage;
            public float  Range;
            public float  AngleDeg = 60f; // полный угол
            public ShareMode Share = ShareMode.Uniform;

            // LoS (по умолчанию выключен — поведение как раньше)
            public bool   RequireLoS = false;
            public bool   WorldOnly  = false;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
        }

        // Наносит урон всем врагам в конусе перед кастером, с распределением по ShareMode.
        public static SpellResult ConeDamage(ISpellRuntime rt, TargetSnapshot caster, IReadOnlyList<TargetSnapshot> candidates, ConeDamageConfig cfg)
        {
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var targets = AreaCollectors.Cone(
                caster,
                candidates,
                cfg.Range,
                cfg.AngleDeg * 0.5f,
                t => rt.IsEnemy(caster, t)
            );

            // LoS-фильтр (если включён)
            if (cfg.RequireLoS)
            {
                var filtered = new List<TargetSnapshot>(targets.Count);
                foreach (var t in targets)
                {
                    bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                    if (ok) filtered.Add(t);
                }
                targets = filtered;
            }

            if (targets.Count == 0) return SpellResult.Fail();

            // Распределение урона
            float[] parts = cfg.Share == ShareMode.Uniform
                ? DamageSharing.Uniform(targets.Count, cfg.TotalDamage)
                : DamageSharing.RadialLinear(caster.Position, targets.Select(t => t.Position).ToList(), cfg.Range, cfg.TotalDamage);

            // Применение
            for (int i = 0; i < targets.Count; i++)
            {
                var tsid = rt.SidOf(targets[i]);
                if (rt.HasImmunity(tsid, cfg.School) || rt.HasImmunity(tsid, "all")) continue;
                var resist = rt.GetResist01(tsid, cfg.School);
                var dmg = MathF.Max(0, parts[i] * (1f - Math.Clamp(resist, 0f, 1f)));
                if (dmg > 0) rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
            }

            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public sealed class ChainDamageConfig
        {
            public int    SpellId;
            public string School = "nature";
            public float  TotalDamage;
            public float  FirstRange = 10f;  // дальность до seed-цели от кастера
            public float  HopRadius  = 7f;   // радиус прыжка между целями
            public int    Count = 3;

            // >>> добавлено для соответствия DamageSharing.ChainByHop <<<
            public float  DecayPerHop01 = 0.15f; // 0.15 => 85% от предыдущей цели

            // LoS-настройки
            public bool   RequireLoSSeed   = false;  // seed (раньше было Soft())
            public bool   RequireLoSEachHop = false; // каждый хоп
            public bool   WorldOnly = false;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
        }

        // Цепочка урона: выбирает seed и затем N-1 хопов ближ. соседей в радиусе HopRadius.
        public static SpellResult ChainDamage(ISpellRuntime rt, TargetSnapshot caster, IReadOnlyList<TargetSnapshot> candidates, ChainDamageConfig cfg)
        {
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var casterSnap = caster;
            var pool = candidates.Where(t => rt.IsEnemy(casterSnap, t)).ToList();

            // seed
            float firstR2 = cfg.FirstRange * cfg.FirstRange;
            TargetSnapshot seed = default;

            foreach (var t in pool)
            {
                if (DistanceSq(casterSnap.Position, t.Position) > firstR2) continue;

                bool losOk = cfg.RequireLoSSeed
                    ? (cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(casterSnap, t) : LineOfSight.Visible(casterSnap, t))
                    : LineOfSight.Soft(casterSnap, t);

                if (!losOk) continue;

                if (seed.Equals(default(TargetSnapshot)) ||
                    DistanceSq(casterSnap.Position, t.Position) < DistanceSq(casterSnap.Position, seed.Position))
                {
                    seed = t;
                }
            }

            if (seed.Equals(default(TargetSnapshot))) return SpellResult.Fail();

            var chain = new List<TargetSnapshot> { seed };
            var current = seed;
            float hop2 = cfg.HopRadius * cfg.HopRadius;

            while (chain.Count < Math.Max(1, cfg.Count))
            {
                TargetSnapshot next = default;
                float bestD2 = float.MaxValue;

                foreach (var t in pool)
                {
                    if (chain.Contains(t)) continue;
                    var d2 = DistanceSq(current.Position, t.Position);
                    if (d2 > hop2) continue;

                    bool losOk = cfg.RequireLoSEachHop
                        ? (cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(current, t) : LineOfSight.Visible(current, t))
                        : LineOfSight.Soft(current, t);

                    if (!losOk) continue;

                    if (d2 < bestD2) { bestD2 = d2; next = t; }
                }

                if (next.Equals(default(TargetSnapshot))) break;
                chain.Add(next);
                current = next;
            }

            // распределение по хопам с затуханием
            var parts = DamageSharing.ChainByHop(chain.Count, cfg.TotalDamage, cfg.DecayPerHop01);

            for (int i = 0; i < chain.Count; i++)
            {
                var tsid = rt.SidOf(chain[i]);
                if (rt.HasImmunity(tsid, cfg.School) || rt.HasImmunity(tsid, "all")) continue;
                var resist = rt.GetResist01(tsid, cfg.School);
                var dmg = MathF.Max(0, parts[i] * (1f - resist));
                if (dmg > 0) rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
            }

            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // helpers
        private static float DistanceSq(in Vector3 a, in Vector3 b)
        { var dx = a.X - b.X; var dy = a.Y - b.Y; var dz = a.Z - b.Z; return dx*dx + dy*dy + dz*dz; }
    }
}