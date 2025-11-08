using System;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    public static partial class LineOfSight
    {
        internal static class Settings
        {
            internal static float HeadZ = 54f;
            internal static float SoftFudge = 8f;
            internal static float CapsuleRadius = 10f;
            internal static int   PathSteps = 8;
            internal static bool  UseCache = true;
            internal static int   CacheMs = 35;
            internal static LoSMask  DefaultMask   = LoSMask.All;
            internal static LoSFilter DefaultFilter = LoSFilter.IgnoreOwner;

            // Grid-cache
            internal static bool  UseGridCache = true;
            internal static float GridSize = 32f; // ~1 тайл/клетка

            internal static TimeSpan CacheTtl => TimeSpan.FromMilliseconds(CacheMs);

            internal static void Apply(
                float? headZ = null, float? softFudge = null, float? capsule = null,
                int? pathSteps = null, bool? useCache = null, int? cacheMs = null,
                LoSMask? mask = null, LoSFilter? filter = null,
                bool? useGrid = null, float? gridSize = null)
            {
                if (headZ.HasValue)      HeadZ = headZ.Value;
                if (softFudge.HasValue)  SoftFudge = softFudge.Value;
                if (capsule.HasValue)    CapsuleRadius = capsule.Value;
                if (pathSteps.HasValue)  PathSteps = Math.Max(1, pathSteps.Value);
                if (useCache.HasValue)   UseCache = useCache.Value;
                if (cacheMs.HasValue)    CacheMs = Math.Max(1, cacheMs.Value);
                if (mask.HasValue)       DefaultMask = mask.Value;
                if (filter.HasValue)     DefaultFilter = filter.Value;
                if (useGrid.HasValue)    UseGridCache = useGrid.Value;
                if (gridSize.HasValue)   GridSize = MathF.Max(1f, gridSize.Value);

                _cache  = new LoSCache(CacheTtl, 4096);
                _gcache = new LoSGridCache(GridSize);
            }
        }
    }
}