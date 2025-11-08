using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using static WarcraftCS2.Spells.Systems.Patterns.Directional; // HitFilter
using WarcraftCS2.Spells.Systems.Core.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Наблюдение за зоной: коллбэки на вход/выход и тик для тех, кто внутри.
    public static class ZoneProc
    {
        public sealed class WatchConfig
        {
            public bool RequireLoSFromCaster = false;
            public bool WorldOnly = false;

            public int   SpellId = 0;        // для идентификации/логов (не обязателен)
            public float Radius = 5f;
            public float Duration = 8f;
            public float TickEvery = 0.2f;
            public bool  Flat = true;
            public HitFilter Filter = HitFilter.All;
            public bool  IncludeSelf = true;

            public Action<TargetSnapshot>? OnEnter;
            public Action<TargetSnapshot>? OnExit;
            public Action<TargetSnapshot>? OnTickInside; // на каждого, кто внутри, каждый тик
        }

        public static void WatchCircle(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot centerRef,
            IReadOnlyList<TargetSnapshot> candidates,
            WatchConfig cfg)
        {
            Vector3 center = centerRef.Position;
            float r2 = cfg.Radius * cfg.Radius;
            float tick = MathF.Max(0.05f, cfg.TickEvery);
            int csid = rt.SidOf(caster);

            var inside = new HashSet<ulong>();
            var lastKnown = new Dictionary<ulong, TargetSnapshot>();

            rt.StartPeriodic(
                csid, csid, cfg.SpellId,
                MathF.Max(0.05f, cfg.Duration),
                tick,
                onTick: () =>
                {
                    var now = new HashSet<ulong>();

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!cfg.IncludeSelf && rt.SidOf(t) == csid) continue;

                        if (cfg.Filter == HitFilter.Allies && !rt.IsAlly(caster, t))  continue;
                        if (cfg.Filter == HitFilter.Enemies && !rt.IsEnemy(caster, t)) continue;

                        var p = t.Position;
                        float dx = p.X - center.X, dy = p.Y - center.Y, dz = p.Z - center.Z;
                        if (cfg.Flat) dz = 0f;
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 > r2) continue;

                        if (cfg.RequireLoSFromCaster)
                        {
                            bool visible = cfg.WorldOnly
                                ? LineOfSight.VisibleWorldOnly(caster, t)
                                : LineOfSight.Visible(caster, t);
                            if (!visible) continue;
                        }

                        int tsid = rt.SidOf(t);
                        ulong uid = (ulong)tsid;
                        now.Add(uid);
                        lastKnown[uid] = t;

                        if (!inside.Contains(uid))
                            cfg.OnEnter?.Invoke(t);

                        cfg.OnTickInside?.Invoke(t);
                    }

                    foreach (var uid in inside)
                    {
                        if (!now.Contains(uid) && lastKnown.TryGetValue(uid, out var snap))
                            cfg.OnExit?.Invoke(snap);
                    }

                    inside = now;
                },
                onEnd: () =>
                {
                    foreach (var uid in inside)
                        if (lastKnown.TryGetValue(uid, out var snap))
                            cfg.OnExit?.Invoke(snap);
                });
        }
    }
}