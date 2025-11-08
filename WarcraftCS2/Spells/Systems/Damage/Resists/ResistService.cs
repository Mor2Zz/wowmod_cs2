using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems.Damage;

namespace WarcraftCS2.Spells.Systems.Damage.Resists
{
    /// Простые резисты по школам (0..1 -> 0%..100% снижения). 
    /// Применяются к входящему урону ДО пайплайна щитов/иммунитетов.
    /// По умолчанию у всех 0 (нет снижения).
    public sealed class ResistService
    {
        // targetSid -> school -> pct (0..1)
        private readonly Dictionary<ulong, Dictionary<DamageSchool, double>> _pct = new();

        public void SetPct(ulong targetSid, DamageSchool school, double pct)
        {
            if (!_pct.TryGetValue(targetSid, out var map))
            {
                map = new Dictionary<DamageSchool, double>();
                _pct[targetSid] = map;
            }
            map[school] = Math.Max(0.0, Math.Min(1.0, pct));
        }

        public double GetPct(ulong targetSid, DamageSchool school)
        {
            if (_pct.TryGetValue(targetSid, out var map) && map.TryGetValue(school, out var pct))
                return pct;
            return 0.0;
        }

        /// Вернёт amount * (1 - resistPct).
        public double Apply(ulong targetSid, DamageSchool school, double amount)
        {
            if (amount <= 0) return 0;
            var pct = GetPct(targetSid, school);
            var mul = 1.0 - pct;
            if (mul < 0) mul = 0;
            return amount * mul;
        }

        public void Clear(ulong targetSid)
        {
            _pct.Remove(targetSid);
        }
    }
}