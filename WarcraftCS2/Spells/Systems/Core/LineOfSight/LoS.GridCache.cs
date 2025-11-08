using System;
using System.Collections.Generic;
using System.Numerics;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    // Долгоживущий grid-кэш по квантизованным клеткам. Чистится ResetCache().
    internal sealed class LoSGridCache
    {
        readonly struct Key : IEquatable<Key>
        {
            public readonly int ax, ay, az, bx, by, bz, flags;
            public Key(int ax, int ay, int az, int bx, int by, int bz, int flags)
            { this.ax=ax; this.ay=ay; this.az=az; this.bx=bx; this.by=by; this.bz=bz; this.flags=flags; }

            public bool Equals(Key o) =>
                ax==o.ax && ay==o.ay && az==o.az && bx==o.bx && by==o.by && bz==o.bz && flags==o.flags;
            public override bool Equals(object? obj) => obj is Key k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = ax; h = (h * 397) ^ ay; h = (h * 397) ^ az;
                    h = (h * 397) ^ bx; h = (h * 397) ^ by; h = (h * 397) ^ bz;
                    h = (h * 397) ^ flags;
                    return h;
                }
            }
        }

        readonly Dictionary<Key, bool> _map = new(4096);
        float _cell;

        public LoSGridCache(float cell) { _cell = MathF.Max(1f, cell); }

        public int Count => _map.Count;
        public float Cell => _cell;

        public void Reconfigure(float cell)
        {
            _cell = MathF.Max(1f, cell);
            _map.Clear();
        }

        static int Q(float v, float cell) => (int)MathF.Floor(v / cell);

        static void Quant(Vector3 v, float cell, out int x, out int y, out int z)
        {
            x = Q(v.X, cell); y = Q(v.Y, cell); z = Q(v.Z, cell);
        }

        public bool TryGet(in Vector3 a, in Vector3 b, int flags, out bool visible)
        {
            Quant(a, _cell, out var ax, out var ay, out var az);
            Quant(b, _cell, out var bx, out var by, out var bz);
            var k = new Key(ax, ay, az, bx, by, bz, flags);
            if (_map.TryGetValue(k, out var v)) { visible = v; return true; }
            // симметрия пары
            var k2 = new Key(bx, by, bz, ax, ay, az, flags);
            if (_map.TryGetValue(k2, out v)) { visible = v; return true; }
            visible = false; return false;
        }

        public void Put(in Vector3 a, in Vector3 b, int flags, bool visible)
        {
            Quant(a, _cell, out var ax, out var ay, out var az);
            Quant(b, _cell, out var bx, out var by, out var bz);
            var k = new Key(ax, ay, az, bx, by, bz, flags);
            _map[k] = visible;
        }

        public void Clear() => _map.Clear();
    }
}