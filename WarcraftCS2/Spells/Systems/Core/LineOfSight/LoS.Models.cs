using System;
using System.Collections.Generic;
using System.Numerics;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    internal static class LoSTargetPoints
    {
        public static IEnumerable<Vector3> ProbePoints(Vector3 to, bool includeCapsule, float capsuleRadius, float headZ)
        {
            float head  = headZ;
            float chest = MathF.Max(12f, head - 14f);
            float pelvis= MathF.Max(6f,  chest - 12f);
            float legs  = MathF.Max(2f,  pelvis - 12f);

            yield return new Vector3(to.X, to.Y, to.Z + head);
            yield return new Vector3(to.X, to.Y, to.Z + chest);
            yield return new Vector3(to.X, to.Y, to.Z + pelvis);
            yield return new Vector3(to.X, to.Y, to.Z + legs);

            if (!includeCapsule || capsuleRadius <= 0.01f)
                yield break;

            const int ring = 8;
            float z = to.Z + chest;
            float step = MathF.Tau / ring;
            for (int i = 0; i < ring; i++)
            {
                float ang = step * i;
                float px = to.X + MathF.Cos(ang) * capsuleRadius;
                float py = to.Y + MathF.Sin(ang) * capsuleRadius;
                yield return new Vector3(px, py, z);
            }
        }

        public static IEnumerable<Vector3> SoftPoints(Vector3 to, float fudge, float headZ)
        {
            float f = MathF.Max(0.01f, fudge);
            yield return to + new Vector3(0, 0, headZ);
            yield return to + new Vector3( f,  0,  f * 0.6f + headZ);
            yield return to + new Vector3(-f,  0,  f * 0.6f + headZ);
            yield return to + new Vector3( 0,  f,  f * 0.6f + headZ);
            yield return to + new Vector3( 0, -f,  f * 0.6f + headZ);
        }

        public static IEnumerable<Vector3> PathSamples(Vector3 from, Vector3 to, int steps)
        {
            steps = steps < 1 ? 1 : steps;
            var dir = to - from;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                yield return from + dir * t;
            }
        }

        // Дополнительные сегменты: top↔bottom и start↔top, как в мастере
        public static IEnumerable<(Vector3 A, Vector3 B)> VerticalSegments(Vector3 basePos, float headZ)
        {
            var bottom = basePos + new Vector3(0,0,MathF.Max(2f, headZ - 52f));
            var top    = basePos + new Vector3(0,0,headZ + 36f);
            yield return (top, bottom);
        }
        public static IEnumerable<(Vector3 A, Vector3 B)> DiagonalSegments(Vector3 from, Vector3 basePos, float headZ)
        {
            var top = basePos + new Vector3(0,0,headZ + 36f);
            yield return (from, top);
        }
    }
}