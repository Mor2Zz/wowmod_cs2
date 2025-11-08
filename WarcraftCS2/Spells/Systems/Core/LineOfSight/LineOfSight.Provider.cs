using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    public static class LineOfSightProvider
    {
        private sealed class SoftAdapter : ILineOfSight
        {
            public bool Has(in TargetSnapshot a, in TargetSnapshot b) => LineOfSight.Soft(a, b);
        }

        private static ILineOfSight _impl;

        static LineOfSightProvider()
        {
            _impl = new SoftAdapter();
            LineOfSight.SetBaseProvider(_impl);
        }

        public static void Set(ILineOfSight impl)
        {
            if (impl == null) return;
            _impl = impl;
            LineOfSight.SetBaseProvider(_impl);
        }

        public static bool Has(in TargetSnapshot a, in TargetSnapshot b) => _impl.Has(a, b);
    }
}