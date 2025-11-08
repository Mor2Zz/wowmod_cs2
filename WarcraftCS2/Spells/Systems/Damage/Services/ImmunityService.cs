using System;
using System.Collections.Generic;

namespace WarcraftCS2.Spells.Systems.Damage.Services
{
    public sealed class ImmunityService
    {
        private readonly Dictionary<ulong, List<Immunity>> _store = new();

        public void Add(ulong steamId, DamageSchoolMask mask, double durationSec, string source)
        {
            var until = DateTime.UtcNow.AddSeconds(durationSec);
            var list = GetList(steamId);
            list.Add(new Immunity { Mask = mask, Until = until, Source = source });
        }

        public bool IsImmune(ulong steamId, DamageSchool school)
        {
            if (!_store.TryGetValue(steamId, out var list) || list.Count == 0)
                return false;

            var now = DateTime.UtcNow;
            var m = school.ToMask();

            for (int i = list.Count - 1; i >= 0; i--)
            {
                var im = list[i];
                if (im.Until <= now) { list.RemoveAt(i); continue; }
                if ((im.Mask & m) != 0) return true;
            }
            return false;
        }

        public void Sweep(DateTime nowUtc)
        {
            foreach (var kv in _store)
            {
                var lst = kv.Value;
                for (int i = lst.Count - 1; i >= 0; i--)
                {
                    if (lst[i].Until <= nowUtc)
                        lst.RemoveAt(i);
                }
            }
        }

        private List<Immunity> GetList(ulong sid)
        {
            if (!_store.TryGetValue(sid, out var list))
            {
                list = new List<Immunity>(capacity: 2);
                _store[sid] = list;
            }
            return list;
        }
    }

    public struct Immunity
    {
        public DamageSchoolMask Mask;
        public DateTime Until;
        public string Source;
    }
}