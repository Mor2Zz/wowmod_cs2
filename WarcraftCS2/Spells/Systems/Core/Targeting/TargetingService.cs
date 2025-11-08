using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.LineOfSight;
using LOS = WarcraftCS2.Spells.Systems.Core.LineOfSight.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Core.Targeting
{
    public sealed class TargetingService
    {
        public (bool ok, List<TargetSnapshot> targets, string reason) ResolveTargets(
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            TargetingPolicy policy,
            Func<int, int, bool> areAllies,
            Func<TargetSnapshot, TargetSnapshot, bool>? hasLoS = null)
        {
            Func<TargetSnapshot, TargetSnapshot, bool> los;
            if (hasLoS is null)
            {
                los = static (c, t) => LOS.Soft(c, t);
            }
            else
            {
                los = hasLoS;
            }

            if (policy.Kind == TargetKind.Self)
                return (true, new List<TargetSnapshot> { caster }, string.Empty);

            var filtered = new List<TargetSnapshot>(candidates.Count);
            foreach (var t in candidates)
            {
                if (policy.ExcludeSelf && t.IsSelf) continue;
                if (policy.RequireAlive && !t.Alive) continue;

                bool ally = areAllies(caster.Team, t.Team);
                bool passKind = policy.Kind switch
                {
                    TargetKind.Ally => ally,
                    TargetKind.Enemy => !ally,
                    TargetKind.Any => true,
                    _ => true
                };
                if (!passKind) continue;

                var dist = Vector3.Distance(caster.Position, t.Position);
                if (policy.Shape != ShapeKind.AoE && dist > policy.Range) continue;

                if (policy.RequireLoS && !los(caster, t)) continue;

                filtered.Add(t);
            }

            if (filtered.Count == 0)
                return (false, new List<TargetSnapshot>(), "Нет подходящих целей");

            switch (policy.Shape)
            {
                case ShapeKind.Single:
                {
                    var best = filtered
                        .Select(t =>
                        {
                            var dir = Vector3.Normalize(t.Position - caster.Position);
                            var cos = SafeDot(caster.Forward, dir);
                            var ang = MathF.Acos(Math.Clamp(cos, -1f, 1f)) * (180f / MathF.PI);
                            var dist = Vector3.Distance(caster.Position, t.Position);
                            return (t, ang, dist);
                        })
                        .OrderBy(x => x.ang).ThenBy(x => x.dist)
                        .First().t;

                    return (true, new List<TargetSnapshot> { best }, string.Empty);
                }

                case ShapeKind.AoE:
                {
                    var inRadius = filtered
                        .Where(t => Vector3.Distance(caster.Position, t.Position) <= policy.Radius)
                        .OrderBy(t => Vector3.Distance(caster.Position, t.Position))
                        .Take(Math.Max(1, policy.MaxTargets))
                        .ToList();

                    if (inRadius.Count == 0)
                        return (false, new List<TargetSnapshot>(), "Нет целей в радиусе");

                    return (true, inRadius, string.Empty);
                }

                case ShapeKind.Cone:
                {
                    float half = policy.AngleDeg * 0.5f;
                    var inCone = filtered
                        .Select(t =>
                        {
                            var dir = Vector3.Normalize(t.Position - caster.Position);
                            var cos = SafeDot(caster.Forward, dir);
                            var ang = MathF.Acos(Math.Clamp(cos, -1f, 1f)) * (180f / MathF.PI);
                            var dist = Vector3.Distance(caster.Position, t.Position);
                            return (t, ang, dist);
                        })
                        .Where(x => x.ang <= half)
                        .OrderBy(x => x.ang).ThenBy(x => x.dist)
                        .Select(x => x.t)
                        .Take(Math.Max(1, policy.MaxTargets))
                        .ToList();

                    if (inCone.Count == 0)
                        return (false, new List<TargetSnapshot>(), "Нет целей в конусе");

                    return (true, inCone, string.Empty);
                }
            }

            return (false, new List<TargetSnapshot>(), "Неподдерживаемая форма");
        }

        private static float SafeDot(in Vector3 a, in Vector3 b)
        {
            var na = a; var nb = b;
            if (na.LengthSquared() < 1e-8f || nb.LengthSquared() < 1e-8f) return 0f;
            na = Vector3.Normalize(na);
            nb = Vector3.Normalize(nb);
            return Vector3.Dot(na, nb);
        }
    }
}