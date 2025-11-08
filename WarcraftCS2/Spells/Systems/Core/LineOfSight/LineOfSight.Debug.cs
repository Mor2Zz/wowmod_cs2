using System.Text;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    public static partial class LineOfSight
    {
        public static string DebugDump()
        {
            var m = Metrics.Snapshot();
            var sb = new StringBuilder(256);

            sb.Append("LoS Settings: ");
            sb.Append("HeadZ=").Append(Settings.HeadZ)
              .Append(", SoftFudge=").Append(Settings.SoftFudge)
              .Append(", CapsuleRadius=").Append(Settings.CapsuleRadius)
              .Append(", PathSteps=").Append(Settings.PathSteps)
              .Append(", UseCache=").Append(Settings.UseCache)
              .Append(", CacheMs=").Append(Settings.CacheMs)
              .Append(", UseGridCache=").Append(Settings.UseGridCache)
              .Append(", GridSize=").Append(Settings.GridSize)
              .Append(", DefaultMask=").Append(Settings.DefaultMask)
              .Append(", DefaultFilter=").Append(Settings.DefaultFilter)
              .AppendLine();

            sb.Append("Caches: ");
            sb.Append("coordCount=").Append(_cacheCountSafe())
              .Append(", gridCount=").Append(_gcacheCountSafe())
              .AppendLine();

            sb.Append("Metrics: ");
            sb.Append("calls=").Append(m.Calls)
              .Append(", cacheHit=").Append(m.CacheHits)
              .Append(", cachePut=").Append(m.CachePuts)
              .Append(", gridHit=").Append(m.GridHits)
              .Append(", gridPut=").Append(m.GridPuts)
              .Append(", multiProbe=").Append(m.MultiProbeAttempts)
              .Append(", softProbe=").Append(m.SoftProbeAttempts)
              .Append(", pathSegs=").Append(m.PathSegments)
              .Append(", ground=").Append(m.GroundChecks)
              .Append(", vertical=").Append(m.VerticalSegments)
              .Append(", baseRays=").Append(m.BaseRays)
              .Append(", filteredRays=").Append(m.FilteredRays);

            return sb.ToString();
        }

        static int _cacheCountSafe()
        {
            try { return _cache != null ? (int)typeof(LoSCache).GetField("_map", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_cache)!.GetType().GetProperty("Count")!.GetValue(typeof(LoSCache).GetField("_map", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(_cache))! : 0; } catch { return 0; }
        }

        static int _gcacheCountSafe()
        {
            try { return _gcache != null ? _gcache.Count : 0; } catch { return 0; }
        }
    }
}