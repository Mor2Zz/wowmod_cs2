using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using LOS = WarcraftCS2.Spells.Systems.Core.LineOfSight.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Core.Targeting
{
    /// Селекторы по world-координатам: сфера и капсула.
    public static class GroundSelectors
    {
        public static List<TargetSnapshot> Sphere(
            in Vector3 center,
            IEnumerable<TargetSnapshot> candidates,
            float radius,
            Func<TargetSnapshot, bool> predicate)
        {
            var o = center; // не захватываем in в лямбдах
            float r2 = radius * radius;
            return candidates
                .Where(predicate)
                .Where(t => DistanceSq(o, t.Position) <= r2)
                .OrderBy(t => DistanceSq(o, t.Position))
                .ToList();
        }

        public static List<TargetSnapshot> Sphere(
            in TargetSnapshot caster,
            in Vector3 center,
            IEnumerable<TargetSnapshot> candidates,
            float radius,
            Func<TargetSnapshot, bool> predicate,
            Func<TargetSnapshot, TargetSnapshot, bool>? hasLoS = null)
        {
            var casterSnap = caster;
            var o = center;
            var los = hasLoS;
            if (los == null) los = (c, t) => LOS.Soft(c, t);

            float r2 = radius * radius;
            return candidates
                .Where(predicate)
                .Where(t => DistanceSq(o, t.Position) <= r2)
                .Where(t => los(casterSnap, t))
                .OrderBy(t => DistanceSq(o, t.Position))
                .ToList();
        }

        public static List<TargetSnapshot> Capsule(
            in Vector3 a,
            in Vector3 b,
            IEnumerable<TargetSnapshot> candidates,
            float radius,
            Func<TargetSnapshot, bool> predicate)
        {
            var A = a; var B = b; var R = radius;
            return candidates
                .Where(predicate)
                .Where(t => InCapsule(A, B, t.Position, R))
                .OrderBy(t => DistanceSqToSegment(A, B, t.Position))
                .ToList();
        }

        public static List<TargetSnapshot> Capsule(
            in TargetSnapshot caster,
            in Vector3 a,
            in Vector3 b,
            IEnumerable<TargetSnapshot> candidates,
            float radius,
            Func<TargetSnapshot, bool> predicate,
            Func<TargetSnapshot, TargetSnapshot, bool>? hasLoS = null)
        {
            var casterSnap = caster;
            var A = a; var B = b; var R = radius;
            var los = hasLoS;
            if (los == null) los = (c, t) => LOS.Soft(c, t);

            return candidates
                .Where(predicate)
                .Where(t => InCapsule(A, B, t.Position, R))
                .Where(t => los(casterSnap, t))
                .OrderBy(t => DistanceSqToSegment(A, B, t.Position))
                .ToList();
        }

        // --- helpers ---
        private static bool InCapsule(in Vector3 a, in Vector3 b, in Vector3 p, float r)
        {
            var ab = b - a; var ap = p - a;
            var abLen2 = Dot(ab, ab) + 1e-6f;
            var t = Dot(ap, ab) / abLen2;
            t = MathF.Max(0, MathF.Min(1, t));
            var closest = a + t * ab;
            return DistanceSq(closest, p) <= r * r;
        }

        private static float DistanceSqToSegment(in Vector3 a, in Vector3 b, in Vector3 p)
        {
            var ab = b - a; var ap = p - a;
            var abLen2 = Dot(ab, ab) + 1e-6f;
            var t = Dot(ap, ab) / abLen2;
            t = MathF.Max(0, MathF.Min(1, t));
            var closest = a + t * ab;
            return DistanceSq(closest, p);
        }

        private static float DistanceSq(in Vector3 x, in Vector3 y)
        { var dx = x.X - y.X; var dy = x.Y - y.Y; var dz = x.Z - y.Z; return dx*dx + dy*dy + dz*dz; }

        private static float Dot(in Vector3 x, in Vector3 y) => x.X*y.X + x.Y*y.Y + x.Z*y.Z;
    }
}