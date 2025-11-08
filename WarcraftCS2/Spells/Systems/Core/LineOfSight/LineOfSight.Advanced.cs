using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    public static partial class LineOfSight
    {
        [Flags]
        public enum LoSFlags
        {
            None            = 0,
            GroundCheck     = 1 << 0,
            PathCheck       = 1 << 1,
            MultiProbe      = 1 << 2,
            CapsuleFallback = 1 << 3,
            SoftCloud       = 1 << 4,
            UseCache        = 1 << 5,
            VerticalExtra   = 1 << 6,
            WorldOnlyMask   = 1 << 7,
            IgnorePlayers   = 1 << 8
        }

        public struct LoSOptions
        {
            public LoSFlags Flags;
            public LoSFilter Filter;
            public LoSMask   Mask;
            public Vector3   GroundPoint;
            public int       PathSteps;
            public float     SoftFudge;
            public float     CapsuleRadius;
            public float     HeadZ;

            public static LoSOptions Default => new LoSOptions
            {
                Flags         = LoSFlags.MultiProbe | LoSFlags.SoftCloud | (Settings.UseCache ? LoSFlags.UseCache : LoSFlags.None),
                Filter        = Settings.DefaultFilter,
                Mask          = Settings.DefaultMask,
                PathSteps     = Settings.PathSteps,
                SoftFudge     = Settings.SoftFudge,
                CapsuleRadius = Settings.CapsuleRadius,
                HeadZ         = Settings.HeadZ
            };

            public LoSOptions WithGround(Vector3 ground) { var c = this; c.Flags |= LoSFlags.GroundCheck; c.GroundPoint = ground; return c; }
            public LoSOptions WithPath(int steps = 8)    { var c = this; c.Flags |= LoSFlags.PathCheck; c.PathSteps = Math.Max(1, steps); return c; }
            public LoSOptions Hard()                     { var c = this; c.Flags &= ~LoSFlags.SoftCloud; return c; }
            public LoSOptions WorldOnly()                { var c = this; c.Flags |= LoSFlags.WorldOnlyMask; c.Mask = LoSMask.WorldOnly; return c; }
            public LoSOptions IgnoreAllPlayers()         { var c = this; c.Flags |= LoSFlags.IgnorePlayers; c.Filter |= LoSFilter.IgnorePlayers; return c; }
            public LoSOptions WithVertical()             { var c = this; c.Flags |= LoSFlags.VerticalExtra; return c; }
        }

        internal static LoSCache     _cache  = new LoSCache(Settings.CacheTtl, 4096);
        internal static LoSGridCache _gcache = new LoSGridCache(Settings.GridSize);

        public static bool Visible(in TargetSnapshot caster, in TargetSnapshot target)
            => CheckAdvanced(caster, target, LoSOptions.Default, out _);

        public static bool VisibleHard(in TargetSnapshot caster, in TargetSnapshot target)
            => CheckAdvanced(caster, target, LoSOptions.Default.Hard(), out _);

        public static bool VisibleOnPath(in TargetSnapshot caster, in TargetSnapshot target, int steps)
            => CheckAdvanced(caster, target, LoSOptions.Default.WithPath(steps), out _);

        public static bool VisibleToGround(in TargetSnapshot caster, Vector3 groundPoint)
            => CheckGround(caster, groundPoint, Settings.SoftFudge, Settings.HeadZ);

        // Шорткаты «мир только»
        public static bool VisibleWorldOnly(in TargetSnapshot caster, in TargetSnapshot target)
            => CheckAdvanced(caster, target, LoSOptions.Default.WorldOnly().IgnoreAllPlayers(), out _);

        public static bool VisibleWorldOnlyHard(in TargetSnapshot caster, in TargetSnapshot target)
            => CheckAdvanced(caster, target, LoSOptions.Default.WorldOnly().IgnoreAllPlayers().Hard(), out _);

        public static bool CheckAdvanced(in TargetSnapshot caster, in TargetSnapshot target, LoSOptions opt, out string fail)
        {
            Metrics.IncCall();
            fail = string.Empty;

            var from  = caster.Position;
            var to    = target.Position;
            int flags = (int)opt.Flags;

            // Pair-cache по SteamId (эпохозависимый)
            if (_pairCache.TryGet(caster.SteamId, target.SteamId, flags, out var pcached))
                return pcached;

            // Grid-cache (долгоживущий)
            if (Settings.UseGridCache && _gcache.TryGet(from, to, flags, out var gcached))
            {
                Metrics.IncGridHit();
                _pairCache.Put(caster.SteamId, target.SteamId, flags, gcached);
                return gcached;
            }

            // TTL-кэш по координатам
            if ((opt.Flags & LoSFlags.UseCache) != 0 && _cache.TryGet(from, to, flags, out var cached))
            {
                Metrics.IncCacheHit();
                _pairCache.Put(caster.SteamId, target.SteamId, flags, cached);
                return cached;
            }

            if (!target.Alive)
            {
                PutAllCaches(from, to, caster.SteamId, target.SteamId, flags, false, opt);
                fail = "dead";
                return false;
            }

            if ((opt.Flags & LoSFlags.GroundCheck) != 0)
            {
                Metrics.IncGround();
                var gp = SnapshotAt(opt.GroundPoint, target.Team);
                if (!HasBaseOrFiltered(caster, gp, opt))
                {
                    PutAllCaches(from, to, caster.SteamId, target.SteamId, flags, false, opt);
                    fail = "ground";
                    return false;
                }
            }

            if ((opt.Flags & LoSFlags.PathCheck) != 0)
            {
                long segs = 0;
                var prev = caster;
                foreach (var p in LoSTargetPoints.PathSamples(from, to, Math.Max(1, opt.PathSteps)))
                {
                    segs++;
                    var snap = SnapshotAt(p, target.Team);
                    if (!HasBaseOrFiltered(prev, snap, opt))
                    {
                        Metrics.AddPathSegments(segs);
                        PutAllCaches(from, to, caster.SteamId, target.SteamId, flags, false, opt);
                        fail = "path";
                        return false;
                    }
                    prev = snap;
                }
                Metrics.AddPathSegments(segs);
            }

            bool ok = false;
            long attempts = 0;

            if ((opt.Flags & LoSFlags.MultiProbe) != 0)
            {
                foreach (var p in LoSTargetPoints.ProbePoints(to, (opt.Flags & LoSFlags.CapsuleFallback) != 0, opt.CapsuleRadius, opt.HeadZ))
                {
                    attempts++;
                    var snap = SnapshotAt(p, target.Team);
                    if (HasBaseOrFiltered(caster, snap, opt)) { ok = true; break; }
                }
                Metrics.AddMultiProbe(attempts);
            }
            else
            {
                var head = to + new Vector3(0, 0, opt.HeadZ);
                if (HasBaseOrFiltered(caster, SnapshotAt(head, target.Team), opt)) ok = true;
            }

            if (!ok && (opt.Flags & LoSFlags.VerticalExtra) != 0)
            {
                long vseg = 0;
                foreach (var seg in LoSTargetPoints.VerticalSegments(to, opt.HeadZ))
                {
                    vseg++;
                    if (RayBaseOrFiltered(seg.A, seg.B, opt)) { ok = true; break; }
                }
                if (!ok)
                {
                    foreach (var seg in LoSTargetPoints.DiagonalSegments(from, to, opt.HeadZ))
                    {
                        vseg++;
                        if (RayBaseOrFiltered(seg.A, seg.B, opt)) { ok = true; break; }
                    }
                }
                Metrics.AddVertical(vseg);
            }

            if (!ok && (opt.Flags & LoSFlags.SoftCloud) != 0)
            {
                long s = 0;
                foreach (var p in LoSTargetPoints.SoftPoints(to, opt.SoftFudge, opt.HeadZ))
                {
                    s++;
                    var snap = SnapshotAt(p, target.Team);
                    if (HasBaseOrFiltered(caster, snap, opt)) { ok = true; break; }
                }
                Metrics.AddSoftProbe(s);
            }

            PutAllCaches(from, to, caster.SteamId, target.SteamId, flags, ok, opt);
            if (!ok) fail = "blocked";
            return ok;
        }

        static void PutAllCaches(Vector3 from, Vector3 to, ulong sidA, ulong sidB, int flags, bool ok, LoSOptions opt)
        {
            if (Settings.UseGridCache) { _gcache.Put(from, to, flags, ok); Metrics.IncGridPut(); }
            if ((opt.Flags & LoSFlags.UseCache) != 0) { _cache.Put(from, to, flags, ok); Metrics.IncCachePut(); }
            _pairCache.Put(sidA, sidB, flags, ok);
        }

        public static bool CheckGround(in TargetSnapshot caster, Vector3 groundPoint, float fudge = 8f, float headZ = 54f)
        {
            var to = SnapshotAt(groundPoint, caster.Team);
            if (BaseHas(caster, to)) return true;

            var soft = groundPoint + new Vector3(fudge, 0, fudge * 0.6f);
            return BaseHas(caster, SnapshotAt(soft, caster.Team));
        }

        static bool HasBaseOrFiltered(in TargetSnapshot a, in TargetSnapshot b, LoSOptions opt)
        {
            if ((opt.Flags & LoSFlags.WorldOnlyMask) != 0 || (opt.Flags & LoSFlags.IgnorePlayers) != 0 || opt.Filter != Settings.DefaultFilter || opt.Mask != Settings.DefaultMask)
            {
                if (TryHasFiltered(a, b, opt.Filter, opt.Mask, out var vis)) return vis;
            }
            return BaseHas(a, b);
        }

        static bool RayBaseOrFiltered(Vector3 from, Vector3 to, LoSOptions opt)
        {
            if ((opt.Flags & LoSFlags.WorldOnlyMask) != 0 || (opt.Flags & LoSFlags.IgnorePlayers) != 0)
            {
                if (TryRay(from, to, opt.Filter, opt.Mask, out var vis)) return vis;
            }
            var snapA = SnapshotAt(from, 0);
            var snapB = SnapshotAt(to, 0);
            return BaseHas(snapA, snapB);
        }

        static TargetSnapshot SnapshotAt(Vector3 pos, int team) => new TargetSnapshot
        {
            Position = pos,
            Forward  = Vector3.UnitX,
            Team     = team,
            Alive    = true,
            IsSelf   = false,
            SteamId  = 0
        };
    }
}