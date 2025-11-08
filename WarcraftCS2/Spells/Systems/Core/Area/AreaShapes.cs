using System;
using System.Numerics;

namespace WarcraftCS2.Spells.Systems.Core.Area
{
    /// Геометрические примитивы: сфера, кольцо, капсула, конус.
    public static class AreaShapes
    {
        public static bool InSphere(in Vector3 origin, in Vector3 point, float radius)
            => DistanceSq(origin, point) <= radius * radius;

        public static bool InRing(in Vector3 origin, in Vector3 point, float innerRadius, float outerRadius)
        {
            var d2 = DistanceSq(origin, point);
            return d2 >= innerRadius * innerRadius && d2 <= outerRadius * outerRadius;
        }

        public static bool InCapsule(in Vector3 a, in Vector3 b, in Vector3 point, float radius)
        {
            var ab = b - a;
            var ap = point - a;
            var t = Dot(ap, ab) / (Dot(ab, ab) + 1e-6f);
            t = MathF.Max(0, MathF.Min(1, t));
            var closest = a + t * ab;
            return DistanceSq(closest, point) <= radius * radius;
        }

        public static bool InCone(in Vector3 origin, in Vector3 forwardNorm, in Vector3 point, float halfAngleRad, float maxDist)
        {
            var to = point - origin;
            var d2 = Dot(to, to);
            if (d2 > maxDist * maxDist) return false;
            var dir = to / MathF.Max(1e-6f, MathF.Sqrt(d2));
            return Dot(forwardNorm, dir) >= MathF.Cos(halfAngleRad);
        }

        // math
        private static float DistanceSq(in Vector3 a, in Vector3 b)
        { var dx = a.X - b.X; var dy = a.Y - b.Y; var dz = a.Z - b.Z; return dx*dx + dy*dy + dz*dz; }

        private static float Dot(in Vector3 a, in Vector3 b) => a.X*b.X + a.Y*b.Y + a.Z*b.Z;
    }
}