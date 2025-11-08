using System;
using System.Collections.Generic;
using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace WarcraftCS2.Spells.Systems.Core.Targeting
{
    public static class Targeting
    {
        public static TargetingPolicy EnemySingle(float range = 750f, bool requireLoS = true)
            => TargetingPolicy.EnemySingle(range, requireLoS);
        public static TargetingPolicy AllySingle(float range = 750f, bool requireLoS = true)
            => TargetingPolicy.AllySingle(range, requireLoS);
        public static TargetingPolicy SelfOnly() => TargetingPolicy.SelfOnly();
        public static TargetingPolicy EnemyAoE(float range = 750f, float radius = 250f, int maxTargets = 5, bool requireLoS = true)
            => TargetingPolicy.EnemyAoE(range, radius, maxTargets, requireLoS);
        public static TargetingPolicy EnemyCone(float range = 750f, float angleDeg = 35f, int maxTargets = 3, bool requireLoS = true)
            => TargetingPolicy.EnemyCone(range, angleDeg, maxTargets, requireLoS);

        public static bool Resolve(
            object spellInstance,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            Func<int, int, bool> areAllies,
            out List<TargetSnapshot> targets,
            out string failReason,
            Func<TargetSnapshot, TargetSnapshot, bool>? hasLoS = null,
            TargetingPolicy? overridePolicy = null)
        {
            return TargetingGate.TryResolveTargets(
                spellInstance, caster, candidates, areAllies,
                out targets, out failReason, hasLoS, overridePolicy);
        }

        // ---- LoS: заглушка, всегда true (реализацию трейсом подменим позже) ----
        public static bool HasLoS(TargetSnapshot from, TargetSnapshot to) => true;

        // ---- быстрые CSS-хелперы ----
        public static CCSPlayerController? TraceEnemyByView(CCSPlayerController caster, float range = 750f, float maxAngleDeg = 45f)
            => TraceByView(caster, range, maxAngleDeg, enemies: true, includeSelf: false);

        public static CCSPlayerController? TraceAllyByView(CCSPlayerController caster, float range = 750f, float maxAngleDeg = 45f, bool includeSelf = false)
            => TraceByView(caster, range, maxAngleDeg, enemies: false, includeSelf: includeSelf);

        public static List<CCSPlayerController> FindEnemiesInRadius(CCSPlayerController center, float radius)
        {
            var result = new List<CCSPlayerController>(16);
            if (center is null || !center.IsValid) return result;

            if (center.PlayerPawn?.Value is not { IsValid: true, AbsOrigin: { } origin })
                return result;

            var centerPos = new Vector3(origin.X, origin.Y, origin.Z);
            var r2 = radius * radius;
            int myTeam = Convert.ToInt32(center.Team);

            foreach (var p in Utilities.GetPlayers())
            {
                if (p is null || !p.IsValid || p == center) continue;
                if (Convert.ToInt32(p.Team) == myTeam) continue;

                if (p.PlayerPawn?.Value is not { IsValid: true, AbsOrigin: { } o2 })
                    continue;

                var pos = new Vector3(o2.X, o2.Y, o2.Z);
                var d2 = (pos - centerPos).LengthSquared();
                if (d2 <= r2) result.Add(p);
            }
            return result;
        }

        public static List<CCSPlayerController> FindEnemiesInCone(CCSPlayerController caster, float range, float angleDeg)
        {
            var list = new List<CCSPlayerController>(16);
            if (caster is null || !caster.IsValid) return list;

            if (caster.PlayerPawn?.Value is not { IsValid: true, AbsOrigin: { } origin, EyeAngles: { } eye })
                return list;

            var casterPos = new Vector3(origin.X, origin.Y, origin.Z);
            var forward   = AngleToForward((float)eye.X, (float)eye.Y);
            int myTeam    = Convert.ToInt32(caster.Team);

            foreach (var p in Utilities.GetPlayers())
            {
                if (p is null || !p.IsValid || p == caster) continue;
                if (Convert.ToInt32(p.Team) == myTeam) continue;

                if (p.PlayerPawn?.Value is not { IsValid: true, AbsOrigin: { } o2 })
                    continue;

                var to = new Vector3(o2.X, o2.Y, o2.Z) - casterPos;
                var dist = to.Length();
                if (dist <= 1e-3f || dist > range) continue;

                var dir = Vector3.Normalize(to);
                var cos = Vector3.Dot(forward, dir);
                var ang = MathF.Acos(Math.Clamp(cos, -1f, 1f)) * (180f / MathF.PI);
                if (ang <= angleDeg) list.Add(p);
            }
            return list;
        }

        private static CCSPlayerController? TraceByView(CCSPlayerController caster, float range, float maxAngleDeg, bool enemies, bool includeSelf)
        {
            if (caster is null || !caster.IsValid) return null;

            if (caster.PlayerPawn?.Value is not { IsValid: true, AbsOrigin: { } origin, EyeAngles: { } eye })
                return null;

            var casterPos = new Vector3(origin.X, origin.Y, origin.Z);
            var forward   = AngleToForward((float)eye.X, (float)eye.Y);
            int myTeam    = Convert.ToInt32(caster.Team);

            CCSPlayerController? best = null;
            float bestAng = float.MaxValue;
            float bestDist = float.MaxValue;

            foreach (var p in Utilities.GetPlayers())
            {
                if (p is null || !p.IsValid) continue;
                if (!includeSelf && p == caster) continue;

                bool sameTeam = Convert.ToInt32(p.Team) == myTeam;
                if (enemies ? sameTeam : !sameTeam) continue;

                if (p.PlayerPawn?.Value is not { IsValid: true, AbsOrigin: { } o2 })
                    continue;

                var to = new Vector3(o2.X, o2.Y, o2.Z) - casterPos;
                var dist = to.Length();
                if (dist <= 1e-3f || dist > range) continue;

                var dir = Vector3.Normalize(to);
                var cos = Vector3.Dot(forward, dir);
                var ang = MathF.Acos(Math.Clamp(cos, -1f, 1f)) * (180f / MathF.PI);
                if (ang > maxAngleDeg) continue;

                if (ang < bestAng || (Math.Abs(ang - bestAng) < 0.001f && dist < bestDist))
                {
                    best = p;
                    bestAng = ang;
                    bestDist = dist;
                }
            }
            return best;
        }

        private static Vector3 AngleToForward(float pitchDeg, float yawDeg)
        {
            float pitch = pitchDeg * (MathF.PI / 180f);
            float yaw   = yawDeg   * (MathF.PI / 180f);
            float cp = MathF.Cos(pitch), sp = MathF.Sin(pitch);
            float cy = MathF.Cos(yaw),   sy = MathF.Sin(yaw);
            var fwd = new Vector3(cp * cy, cp * sy, -sp);
            if (fwd.LengthSquared() > 1e-6f)
                fwd = Vector3.Normalize(fwd);
            return fwd;
        }
    }
}