using System;
using System.Collections.Generic;
using System.Numerics;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    internal sealed class LoSCache
    {
        readonly struct Key : System.IEquatable<Key>
        {
            readonly int ax, ay, az, bx, by, bz, flags;
            public Key(Vector3 a, Vector3 b, int f)
            {
                ax = (int)MathF.Round(a.X * 100f);
                ay = (int)MathF.Round(a.Y * 100f);
                az = (int)MathF.Round(a.Z * 100f);
                bx = (int)MathF.Round(b.X * 100f);
                by = (int)MathF.Round(b.Y * 100f);
                bz = (int)MathF.Round(b.Z * 100f);
                flags = f;
            }
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

        readonly struct Entry
        {
            public readonly bool Visible;
            public readonly long Expire;
            public Entry(bool v, long e) { Visible = v; Expire = e; }
        }

        readonly Dictionary<Key, Entry> _map = new(2048);
        readonly long _ttl;
        readonly int _max;

        public LoSCache(TimeSpan ttl, int maxEntries = 4096)
        {
            _ttl = ttl.Ticks;
            _max = Math.Max(1024, maxEntries);
        }

        public bool TryGet(Vector3 a, Vector3 b, int flags, out bool visible)
        {
            var k = new Key(a, b, flags);
            if (_map.TryGetValue(k, out var e))
            {
                if (DateTime.UtcNow.Ticks <= e.Expire) { visible = e.Visible; return true; }
                _map.Remove(k);
            }
            visible = false;
            return false;
        }

        public void Put(Vector3 a, Vector3 b, int flags, bool visible)
        {
            if (_map.Count >= _max) _map.Clear();
            var k = new Key(a, b, flags);
            _map[k] = new Entry(visible, DateTime.UtcNow.Ticks + _ttl);
        }
    }
}