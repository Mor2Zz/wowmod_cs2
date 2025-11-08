using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    public static partial class LineOfSight
    {
        static ILineOfSight? _baseImpl;
        static readonly PairCacheU64 _pairCache = new PairCacheU64();

        internal static void SetBaseProvider(ILineOfSight impl)
        {
            _baseImpl = impl;
        }

        internal static bool BaseHas(in TargetSnapshot a, in TargetSnapshot b)
        {
            var impl = _baseImpl;
            return impl != null ? impl.Has(a, b) : Soft(a, b);
        }

        internal static bool TryHasFiltered(in TargetSnapshot a, in TargetSnapshot b, LoSFilter filter, LoSMask mask, out bool visible)
        {
            var impl = _baseImpl;
            if (impl is IFilteredLineOfSight f)
            {
                visible = f.HasFiltered(a, b, filter, mask);
                return true;
            }
            visible = false;
            return false;
        }

        internal static bool TryRay(System.Numerics.Vector3 from, System.Numerics.Vector3 to, LoSFilter filter, LoSMask mask, out bool unobstructed)
        {
            var impl = _baseImpl;
            if (impl is IFilteredLineOfSight f)
            {
                unobstructed = f.Ray(from, to, filter, mask);
                return true;
            }
            unobstructed = false;
            return false;
        }

        public static void ResetCache()
        {
            _cache  = new LoSCache(Settings.CacheTtl, 4096);
            _gcache = new LoSGridCache(Settings.GridSize);
            _pairCache.Clear();
        }

        public static void SetEpoch(long epoch) => _pairCache.SetEpoch(epoch);

        public static LoSMetrics GetMetrics() => Metrics.Snapshot();

        public static void ResetMetrics() => Metrics.Reset();
    }
}