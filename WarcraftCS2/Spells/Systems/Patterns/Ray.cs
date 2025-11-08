using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using LOS = WarcraftCS2.Spells.Systems.Core.LineOfSight.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    public static class Ray
    {
        public enum HitFilter { Enemies, Allies, All }

        public sealed class RayDamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  Damage;

            public float  Range = 800f;
            public float  Radius = 16f;
            public int    MaxTargets = 1;

            public bool   WorldOnly = false;
            public bool   RequireLoS = true;
            public HitFilter Filter = HitFilter.Enemies;

            public float  Mana = 0f;
            public float  Gcd = 0f;
            public float  Cooldown = 0f;
        }

        static bool PassesFilter(ISpellRuntime rt, in TargetSnapshot caster, in TargetSnapshot t, HitFilter f)
            => f switch
            {
                HitFilter.Enemies => rt.IsEnemy(caster, t),
                HitFilter.Allies  => rt.IsAlly(caster, t),
                _                 => true
            };

        static float Dot(in Vector3 a, in Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        static float DistancePointToLine(in Vector3 p, in Vector3 origin, in Vector3 dir, out float t)
        {
            var d = dir;
            var len2 = d.LengthSquared();
            if (len2 < 1e-6f)
            {
                t = 0f;
                return (p - origin).Length();
            }
            var nd = Vector3.Normalize(d);
            var v = p - origin;
            t = Dot(v, nd);
            if (t < 0f) { t = 0f; return (p - origin).Length(); }
            var closest = origin + nd * t;
            return (p - closest).Length();
        }

        public static SpellResult Damage(ISpellRuntime rt, in TargetSnapshot caster, IReadOnlyList<TargetSnapshot> candidates, RayDamageConfig cfg)
        {
            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            var dir = caster.Forward;
            if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX; else dir = Vector3.Normalize(dir);
            var origin = caster.Position;

            var hits = new List<(TargetSnapshot t, float tpos)>(16);

            foreach (var cand in candidates)
            {
                if (!cand.Alive) continue;
                if (!PassesFilter(rt, caster, cand, cfg.Filter)) continue;

                float t;
                var dist = DistancePointToLine(cand.Position, origin, dir, out t);
                if (t < 0f || t > cfg.Range) continue;
                if (dist > cfg.Radius) continue;

                if (cfg.RequireLoS)
                {
                    bool los = cfg.WorldOnly ? LOS.VisibleWorldOnly(caster, cand) : LOS.Visible(caster, cand);
                    if (!los) continue;
                }

                hits.Add((cand, t));
            }

            if (hits.Count == 0) return SpellResult.Fail();

            hits.Sort((x, y) => x.tpos.CompareTo(y.tpos));
            int take = cfg.MaxTargets <= 0 ? 1 : Math.Min(cfg.MaxTargets, hits.Count);

            for (int i = 0; i < take; i++)
            {
                var tgt  = hits[i].t;
                int tsid = rt.SidOf(tgt);
                rt.DealDamage(csid, tsid, cfg.SpellId, cfg.Damage, cfg.School);
            }

            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}