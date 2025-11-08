using System.Threading;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    public readonly struct LoSMetrics
    {
        public readonly long Calls;
        public readonly long CacheHits;
        public readonly long CachePuts;
        public readonly long GridHits;
        public readonly long GridPuts;
        public readonly long MultiProbeAttempts;
        public readonly long SoftProbeAttempts;
        public readonly long PathSegments;
        public readonly long GroundChecks;
        public readonly long VerticalSegments;
        public readonly long BaseRays;
        public readonly long FilteredRays;

        public LoSMetrics(long calls, long cacheHits, long cachePuts, long gridHits, long gridPuts,
                          long multiProbe, long softProbe, long pathSegs, long ground, long vertical,
                          long baseRays, long filteredRays)
        {
            Calls = calls; CacheHits = cacheHits; CachePuts = cachePuts; GridHits = gridHits; GridPuts = gridPuts;
            MultiProbeAttempts = multiProbe; SoftProbeAttempts = softProbe;
            PathSegments = pathSegs; GroundChecks = ground; VerticalSegments = vertical;
            BaseRays = baseRays; FilteredRays = filteredRays;
        }
    }

    internal static class Metrics
    {
        static long _calls, _cacheHits, _cachePuts, _gridHits, _gridPuts,
                    _multiProbeAttempts, _softProbeAttempts, _pathSegments, _groundChecks, _verticalSegments, _baseRays, _filteredRays;

        public static void IncCall()                => Interlocked.Increment(ref _calls);
        public static void IncCacheHit()            => Interlocked.Increment(ref _cacheHits);
        public static void IncCachePut()            => Interlocked.Increment(ref _cachePuts);
        public static void IncGridHit()             => Interlocked.Increment(ref _gridHits);
        public static void IncGridPut()             => Interlocked.Increment(ref _gridPuts);
        public static void AddMultiProbe(long n)    => Interlocked.Add(ref _multiProbeAttempts, n);
        public static void AddSoftProbe(long n)     => Interlocked.Add(ref _softProbeAttempts, n);
        public static void AddPathSegments(long n)  => Interlocked.Add(ref _pathSegments, n);
        public static void IncGround()              => Interlocked.Increment(ref _groundChecks);
        public static void AddVertical(long n)      => Interlocked.Add(ref _verticalSegments, n);
        public static void IncBaseRay()             => Interlocked.Increment(ref _baseRays);
        public static void IncFilteredRay()         => Interlocked.Increment(ref _filteredRays);

        public static LoSMetrics Snapshot() => new LoSMetrics(
            Interlocked.Read(ref _calls),
            Interlocked.Read(ref _cacheHits),
            Interlocked.Read(ref _cachePuts),
            Interlocked.Read(ref _gridHits),
            Interlocked.Read(ref _gridPuts),
            Interlocked.Read(ref _multiProbeAttempts),
            Interlocked.Read(ref _softProbeAttempts),
            Interlocked.Read(ref _pathSegments),
            Interlocked.Read(ref _groundChecks),
            Interlocked.Read(ref _verticalSegments),
            Interlocked.Read(ref _baseRays),
            Interlocked.Read(ref _filteredRays)
        );

        public static void Reset()
        {
            Interlocked.Exchange(ref _calls, 0);
            Interlocked.Exchange(ref _cacheHits, 0);
            Interlocked.Exchange(ref _cachePuts, 0);
            Interlocked.Exchange(ref _gridHits, 0);
            Interlocked.Exchange(ref _gridPuts, 0);
            Interlocked.Exchange(ref _multiProbeAttempts, 0);
            Interlocked.Exchange(ref _softProbeAttempts, 0);
            Interlocked.Exchange(ref _pathSegments, 0);
            Interlocked.Exchange(ref _groundChecks, 0);
            Interlocked.Exchange(ref _verticalSegments, 0);
            Interlocked.Exchange(ref _baseRays, 0);
            Interlocked.Exchange(ref _filteredRays, 0);
        }
    }
}