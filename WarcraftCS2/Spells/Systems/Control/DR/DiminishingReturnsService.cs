using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems.Damage.Services; // AuraCategory

namespace WarcraftCS2.Spells.Systems.Status
{
    /// Трекинг диминишинга: на игроке для каждой DR-группы хранится стадия и время истечения окна.
    /// Apply(...) возвращает урезанную длительность и продвигает стадию.
    public sealed class DiminishingReturnsService
    {
        private sealed class DRState
        {
            public int Stage;          // 0..N (>=3 => immune)
            public DateTime UntilUtc;  // когда обнулить стадию
        }

        // targetSid -> group -> state
        private readonly Dictionary<ulong, Dictionary<DRGroup, DRState>> _store = new();

        /// Применить DR к длительности контроля (Stun/Root/Silence). Для Slow возвращает baseDuration как есть.
        public double Apply(ulong targetSid, AuraCategory categories, double baseDurationSec)
        {
            if (baseDurationSec <= 0) return 0;
            if (!DiminishingReturnsConfig.TryMap(categories, out var group))
                return baseDurationSec; // эта категория не участвует в DR

            var now = DateTime.UtcNow;
            var slot = GetSlot(targetSid, group);

            // если окно истекло — сбросить стадию
            if (now >= slot.UntilUtc)
                slot.Stage = 0;

            var stage = slot.Stage;
            if (stage < 0) stage = 0;
            if (stage >= DiminishingReturnsConfig.Multipliers.Length)
                stage = DiminishingReturnsConfig.Multipliers.Length - 1;

            var mult = DiminishingReturnsConfig.Multipliers[stage];
            var adjusted = baseDurationSec * mult;

            // продвинуть стадию и продлить окно
            slot.Stage = Math.Min(stage + 1, 3); // 3 => иммун
            slot.UntilUtc = now.AddSeconds(DiminishingReturnsConfig.WindowSeconds);

            return adjusted <= 0 ? 0 : adjusted;
        }

        public void Sweep(DateTime nowUtc)
        {
            foreach (var kv in _store)
            {
                var byGroup = kv.Value;
                foreach (var g in new List<DRGroup>(byGroup.Keys))
                {
                    var s = byGroup[g];
                    if (nowUtc >= s.UntilUtc)
                        byGroup.Remove(g);
                }
            }
        }

        private DRState GetSlot(ulong sid, DRGroup group)
        {
            if (!_store.TryGetValue(sid, out var map))
            {
                map = new Dictionary<DRGroup, DRState>();
                _store[sid] = map;
            }
            if (!map.TryGetValue(group, out var state))
            {
                state = new DRState { Stage = 0, UntilUtc = DateTime.UtcNow };
                map[group] = state;
            }
            return state;
        }
    }
}