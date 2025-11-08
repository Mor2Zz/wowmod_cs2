using System;
using System.Collections.Generic;

namespace WarcraftCS2.Spells.Systems.Core.LineOfSight
{
    // Кэш результата по паре (casterSteamId, targetSteamId) на текущую "эпоху" (например, тик).
    internal sealed class PairCacheU64
    {
        struct Key : IEquatable<Key>
        {
            public readonly ulong A;
            public readonly ulong B;
            public readonly int Flags;

            public Key(ulong a, ulong b, int f)
            {
                if (a <= b) { A = a; B = b; } else { A = b; B = a; }
                Flags = f;
            }

            public bool Equals(Key o) => A == o.A && B == o.B && Flags == o.Flags;
            public override bool Equals(object? obj) => obj is Key k && Equals(k);

            public override int GetHashCode()
            {
                unchecked
                {
                    int ha = (int)(A ^ (A >> 32));
                    int hb = (int)(B ^ (B >> 32));
                    int h = ha;
                    h = (h * 397) ^ hb;
                    h = (h * 397) ^ Flags;
                    return h;
                }
            }
        }

        long _epoch;
        readonly Dictionary<Key, bool> _map = new(512);

        public void SetEpoch(long epoch)
        {
            if (epoch != _epoch) { _epoch = epoch; _map.Clear(); }
        }

        public void Clear() => _map.Clear();

        public bool TryGet(ulong a, ulong b, int flags, out bool visible)
        {
            if (a == 0UL || b == 0UL) { visible = false; return false; }
            return _map.TryGetValue(new Key(a, b, flags), out visible);
        }

        public void Put(ulong a, ulong b, int flags, bool visible)
        {
            if (a == 0UL || b == 0UL) return;
            _map[new Key(a, b, flags)] = visible;
        }
    }
}