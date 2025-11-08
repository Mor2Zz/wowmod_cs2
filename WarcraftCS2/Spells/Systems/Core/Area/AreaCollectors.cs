using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using LOS = WarcraftCS2.Spells.Systems.Core.LineOfSight.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Core.Area
{
    /// Коллекторы целей поверх TargetSnapshot с геометрией и LoS.
    public static class AreaCollectors
    {
        public static List<TargetSnapshot> Sphere(
            in TargetSnapshot center,
            IEnumerable<TargetSnapshot> candidates,
            float radius,
            Func<TargetSnapshot, bool> predicate,
            Func<TargetSnapshot, TargetSnapshot, bool>? hasLoS = null)
        {
            var centerSnap = center;
            var los = hasLoS;
            if (los == null) los = (c, t) => LOS.Soft(c, t);

            var o = centerSnap.Position;
            float r2 = radius * radius;

            return candidates
                .Where(predicate)
                .Where(t => DistanceSq(o, t.Position) <= r2)
                .Where(t => los(centerSnap, t))
                .OrderBy(t => DistanceSq(o, t.Position))
                .ToList();
        }

        public static List<TargetSnapshot> Ring(
            in TargetSnapshot center,
            IEnumerable<TargetSnapshot> candidates,
            float innerRadius,
            float outerRadius,
            Func<TargetSnapshot, bool> predicate,
            Func<TargetSnapshot, TargetSnapshot, bool>? hasLoS = null)
        {
            var centerSnap = center;
            var los = hasLoS;
            if (los == null) los = (c, t) => LOS.Soft(c, t);

            var o = centerSnap.Position;
            float in2 = innerRadius * innerRadius;
            float out2 = outerRadius * outerRadius;

            return candidates
                .Where(predicate)
                .Where(t => { var d2 = DistanceSq(o, t.Position); return d2 >= in2 && d2 <= out2; })
                .Where(t => los(centerSnap, t))
                .OrderBy(t => DistanceSq(o, t.Position))
                .ToList();
        }

        public static List<TargetSnapshot> Cone(
            in TargetSnapshot caster,
            IEnumerable<TargetSnapshot> candidates,
            float range,
            float halfAngleDeg,
            Func<TargetSnapshot, bool> predicate,
            Func<TargetSnapshot, TargetSnapshot, bool>? hasLoS = null)
        {
            var casterSnap = caster;
            var los = hasLoS;
            if (los == null) los = (c, t) => LOS.Soft(c, t);

            var o = casterSnap.Position;
            var f = casterSnap.Forward;
            if (f.LengthSquared() < 1e-8f) f = Vector3.UnitX; else f = Vector3.Normalize(f);
            var half = halfAngleDeg * MathF.PI / 180f;
            var cosMin = MathF.Cos(half);
            var r2 = range * range;

            return candidates
                .Where(predicate)
                .Where(t =>
                {
                    var to = t.Position - o; var d2 = to.LengthSquared();
                    if (d2 > r2) return false;
                    var dir = to / MathF.Max(1e-6f, MathF.Sqrt(d2));
                    return Vector3.Dot(f, dir) >= cosMin;
                })
                .Where(t => los(casterSnap, t))
                .OrderBy(t => DistanceSq(o, t.Position))
                .ToList();
        }

        private static float DistanceSq(in Vector3 a, in Vector3 b)
        { var dx = a.X - b.X; var dy = a.Y - b.Y; var dz = a.Z - b.Z; return dx*dx + dy*dy + dz*dz; }
    }
}