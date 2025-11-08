using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using LOS = WarcraftCS2.Spells.Systems.Core.LineOfSight.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Core.Area
{
    /// Отбор целей для AoE спеллов: конус и цепочка.
    public static class AreaTargeting
    {
        public static List<TargetSnapshot> Cone(
            in TargetSnapshot caster,
            IEnumerable<TargetSnapshot> candidates,
            float range,
            float halfAngleDeg,
            Func<TargetSnapshot, bool> isEnemy,
            Func<TargetSnapshot, TargetSnapshot, bool>? hasLoS = null)
        {
            var casterSnap = caster;
            var los = hasLoS;
            if (los == null) los = (c, t) => LOS.Soft(c, t);

            var o = casterSnap.Position;
            var f = casterSnap.Forward;
            if (f.LengthSquared() < 1e-8f) f = Vector3.UnitX; else f = Vector3.Normalize(f);

            float cosMin = MathF.Cos(halfAngleDeg * MathF.PI / 180f);
            float range2 = range * range;

            return candidates
                .Where(isEnemy)
                .Where(t =>
                {
                    var to = t.Position - o;
                    if (to.LengthSquared() > range2) return false;
                    var dir = Vector3.Normalize(to);
                    if (Vector3.Dot(f, dir) <= cosMin) return false;
                    return los(casterSnap, t);
                })
                .OrderBy(t => DistanceSq(o, t.Position))
                .ToList();
        }

        public static List<TargetSnapshot> Chain(
            in TargetSnapshot caster,
            IEnumerable<TargetSnapshot> candidates,
            float firstRange,
            int count,
            float hopRadius,
            Func<TargetSnapshot, bool> isEnemy,
            Func<TargetSnapshot, TargetSnapshot, bool>? hasLoS = null)
        {
            var casterSnap = caster;
            var los = hasLoS;
            if (los == null) los = (c, t) => LOS.Soft(c, t);

            count = Math.Max(1, count);
            var pool = candidates.Where(isEnemy).ToList();

            var o = casterSnap.Position;
            float firstRange2 = firstRange * firstRange;

            var seed = pool
                .Where(t => DistanceSq(o, t.Position) <= firstRange2 && los(casterSnap, t))
                .OrderBy(t => DistanceSq(o, t.Position))
                .FirstOrDefault();

            if (seed.Equals(default(TargetSnapshot)))
                return new List<TargetSnapshot>();

            var chain = new List<TargetSnapshot> { seed };
            var current = seed;
            float hop2 = hopRadius * hopRadius;

            while (chain.Count < count)
            {
                var next = pool
                    .Where(t => !Contains(chain, t))
                    .Where(t => DistanceSq(current.Position, t.Position) <= hop2)
                    .OrderBy(t => DistanceSq(current.Position, t.Position))
                    .FirstOrDefault(t => los(current, t));

                if (next.Equals(default(TargetSnapshot)))
                    break;

                chain.Add(next);
                current = next;
            }

            return chain;
        }

        private static float DistanceSq(in Vector3 a, in Vector3 b)
        { var dx = a.X - b.X; var dy = a.Y - b.Y; var dz = a.Z - b.Z; return dx*dx + dy*dy + dz*dz; }

        private static bool Contains(List<TargetSnapshot> list, in TargetSnapshot item)
            => list.Contains(item); // для struct ок; для class — можно заменить на сравнение по Sid
    }
}