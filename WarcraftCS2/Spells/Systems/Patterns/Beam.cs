using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using LOS = WarcraftCS2.Spells.Systems.Core.LineOfSight.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    public static class Beam
    {
        public enum HitFilter { Enemies, Allies, All }

        public sealed class BeamDamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  DamagePerTick;
            public float  Duration;
            public float  TickEvery = 0.2f;

            public float  Range = 800f;
            public float  Radius = 24f;
            public int    MaxTargets = 8;

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

        static float DistancePointToSegment(in Vector3 p, in Vector3 a, in Vector3 b, out float t)
        {
            var ab = b - a;
            var ap = p - a;
            var abLen2 = ab.LengthSquared();
            if (abLen2 <= 1e-6f) { t = 0f; return (p - a).Length(); }
            t = Dot(ap, ab) / abLen2;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            var closest = a + ab * t;
            return (p - closest).Length();
        }

        public static SpellResult Damage(ISpellRuntime rt, in TargetSnapshot caster, IReadOnlyList<TargetSnapshot> candidates, BeamDamageConfig cfg)
        {
            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Duration <= 0f || cfg.TickEvery <= 0f || cfg.DamagePerTick <= 0f) return SpellResult.Fail();

            var dir = caster.Forward;
            if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX; else dir = Vector3.Normalize(dir);

            var start = caster.Position;
            var end   = start + dir * cfg.Range;

            // важное: локальная копия, чтобы не захватывать параметр 'in' в лямбде
            var casterSnap = caster;

            var handle = rt.StartPeriodic(
                csid,            // srcSid
                csid,            // tgtSid
                cfg.SpellId,     // spellId
                cfg.Duration,
                cfg.TickEvery,
                onTick: () =>
                {
                    var s = start;
                    var e = end;

                    var hits = new List<(TargetSnapshot t, float tpos)>(16);
                    foreach (var cand in candidates)
                    {
                        if (!cand.Alive) continue;
                        if (!PassesFilter(rt, in casterSnap, in cand, cfg.Filter)) continue;

                        float t;
                        var d = DistancePointToSegment(cand.Position, s, e, out t);
                        if (d > cfg.Radius) continue;

                        if (cfg.RequireLoS)
                        {
                            bool los = cfg.WorldOnly
                                ? LOS.VisibleWorldOnly(in casterSnap, in cand)
                                : LOS.Visible(in casterSnap, in cand);
                            if (!los) continue;
                        }

                        hits.Add((cand, t));
                    }

                    if (hits.Count == 0) return;

                    hits.Sort((x, y) => x.tpos.CompareTo(y.tpos));
                    int count = cfg.MaxTargets <= 0 ? hits.Count : Math.Min(cfg.MaxTargets, hits.Count);

                    for (int i = 0; i < count; i++)
                    {
                        var tgt  = hits[i].t;
                        int tsid = rt.SidOf(tgt);
                        rt.DealDamage(csid, tsid, cfg.SpellId, cfg.DamagePerTick, cfg.School);
                    }
                },
                onEnd: () => { }
            );

            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}