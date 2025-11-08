using System;
using System.Collections.Generic;

namespace WarcraftCS2.Spells.Systems.Damage.Services
{
    public sealed class ShieldService
    {
        private readonly Dictionary<ulong, List<Shield>> _store = new();

        public void Add(ulong steamId, double amount, double durationSec, string source, int priority = 0)
        {
            var until = DateTime.UtcNow.AddSeconds(durationSec);
            var list = GetList(steamId);
            list.Add(new Shield { Amount = amount, Until = until, Source = source, Priority = priority });
            // самый высокий приоритет — раньше
            list.Sort(static (a, b) => b.Priority.CompareTo(a.Priority));
        }

        /// <summary>
        /// Применяет щиты к входящему урону.
        /// Возвращает остаток урона после абсорба и сумму поглощённого.
        /// </summary>
        public double Apply(ulong steamId, double incoming, out double absorbedTotal)
        {
            absorbedTotal = 0;
            if (incoming <= 0) return 0;

            if (!_store.TryGetValue(steamId, out var list) || list.Count == 0)
                return incoming;

            var now = DateTime.UtcNow;

            for (int i = 0; i < list.Count && incoming > 0; )
            {
                var s = list[i];

                if (s.Until <= now || s.Amount <= 0)
                {
                    list.RemoveAt(i);
                    continue;
                }

                var absorb = Math.Min(s.Amount, incoming);
                s.Amount -= absorb;
                incoming  -= absorb;
                absorbedTotal += absorb;

                if (s.Amount <= 0 || s.Until <= now)
                {
                    list.RemoveAt(i);
                }
                else
                {
                    list[i] = s;
                    i++; // переходим к следующему только если этот щит остался
                }
            }

            return incoming;
        }

        public void Sweep(DateTime nowUtc)
        {
            foreach (var kv in _store)
            {
                var lst = kv.Value;
                for (int i = lst.Count - 1; i >= 0; i--)
                {
                    var s = lst[i];
                    if (s.Until <= nowUtc || s.Amount <= 0)
                        lst.RemoveAt(i);
                }
            }
        }

        private List<Shield> GetList(ulong sid)
        {
            if (!_store.TryGetValue(sid, out var list))
            {
                list = new List<Shield>(capacity: 2);
                _store[sid] = list;
            }
            return list;
        }
    }

    public struct Shield
    {
        public double Amount;
        public DateTime Until;
        public string Source;
        public int Priority;
    }
}