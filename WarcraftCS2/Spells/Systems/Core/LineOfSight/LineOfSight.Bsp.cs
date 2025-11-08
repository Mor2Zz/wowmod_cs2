using System;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    public sealed class BspLineOfSight : IFilteredLineOfSight
    {
        readonly Func<Vector3, Vector3, bool>? _rayBasic;
        readonly Func<Vector3, Vector3, LoSMask, bool>? _rayMask;
        readonly Func<Vector3, Vector3, LoSFilter, LoSMask, bool>? _rayFiltered;
        readonly float _headZ;

        public BspLineOfSight(Func<Vector3, Vector3, bool> traceUnobstructed, float headZ = 54f)
        {
            _rayBasic = traceUnobstructed ?? throw new ArgumentNullException(nameof(traceUnobstructed));
            _headZ = headZ;
        }

        public BspLineOfSight(Func<Vector3, Vector3, LoSMask, bool> traceWithMask, float headZ = 54f)
        {
            _rayMask = traceWithMask ?? throw new ArgumentNullException(nameof(traceWithMask));
            _headZ = headZ;
        }

        public BspLineOfSight(Func<Vector3, Vector3, LoSFilter, LoSMask, bool> traceFiltered, float headZ = 54f)
        {
            _rayFiltered = traceFiltered ?? throw new ArgumentNullException(nameof(traceFiltered));
            _headZ = headZ;
        }

        public bool Has(in TargetSnapshot a, in TargetSnapshot b)
        {
            var from = a.Position;
            var to   = b.Position + new Vector3(0,0,_headZ);
            return Ray(from, to, LoSFilter.IgnoreOwner, LoSMask.All);
        }

        public bool HasFiltered(in TargetSnapshot a, in TargetSnapshot b, LoSFilter filter, LoSMask mask)
        {
            var from = a.Position;
            var to   = b.Position + new Vector3(0,0,_headZ);
            return Ray(from, to, filter, mask);
        }

        public bool Ray(in Vector3 from, in Vector3 to, LoSFilter filter, LoSMask mask)
        {
            if (_rayFiltered != null) { Metrics.IncFilteredRay(); return _rayFiltered(from, to, filter, mask); }
            if (_rayMask     != null) { Metrics.IncBaseRay();     return _rayMask(from, to, mask); }
            if (_rayBasic    != null) { Metrics.IncBaseRay();     return _rayBasic(from, to); }
            return true;
        }
    }
}