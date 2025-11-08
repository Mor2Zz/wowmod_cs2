using System;
using System.Collections.Generic;

namespace WarcraftCS2.Spells.Systems.Damage.Services
{
    // Битовая маска категорий для диспелов/очистки
    [Flags]
    public enum AuraCategory
    {
        None     = 0,
        Magic    = 1 << 0,
        Poison   = 1 << 1,
        Disease  = 1 << 2,
        Curse    = 1 << 3,
        Physical = 1 << 4,
        Root     = 1 << 5,
        Slow     = 1 << 6,
        Stun     = 1 << 7,
        Silence  = 1 << 8
    }

    public sealed class DispelService
    {
        private readonly Dictionary<ulong, List<Dispellable>> _auras = new();

        public void AddAura(ulong sid, string auraId, AuraCategory cat, double durationSec)
        {
            var until = DateTime.UtcNow.AddSeconds(durationSec);
            var list = GetList(sid);
            list.Add(new Dispellable { AuraId = auraId, Categories = cat, Until = until });
        }

        public int Dispel(ulong sid, AuraCategory allowMask, int maxCount = 1)
        {
            if (!_auras.TryGetValue(sid, out var list) || list.Count == 0)
                return 0;

            var now = DateTime.UtcNow;
            int removed = 0;

            for (int i = list.Count - 1; i >= 0 && removed < maxCount; i--)
            {
                var a = list[i];
                if (a.Until <= now) { list.RemoveAt(i); continue; }
                if ((a.Categories & allowMask) != 0)
                {
                    list.RemoveAt(i);
                    removed++;
                }
            }

            return removed;
        }

        public void Sweep(DateTime nowUtc)
        {
            foreach (var kv in _auras)
            {
                var lst = kv.Value;
                for (int i = lst.Count - 1; i >= 0; i--)
                    if (lst[i].Until <= nowUtc) lst.RemoveAt(i);
            }
        }

        private List<Dispellable> GetList(ulong sid)
        {
            if (!_auras.TryGetValue(sid, out var list))
            {
                list = new List<Dispellable>(capacity: 4);
                _auras[sid] = list;
            }
            return list;
        }
    }

    public struct Dispellable
    {
        public string AuraId;
        public AuraCategory Categories;
        public DateTime Until;
    }
}