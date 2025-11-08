using System;
using WarcraftCS2.Spells.Systems.Damage.Services;

namespace WarcraftCS2.Spells.Systems.Status
{
    /// Агрегатор поведения на основе аур: rooted / stunned / silenced / speed multiplier.
    /// Для Slow используется максимальная величина Magnitude среди активных аур категории Slow.
    public sealed class ControlService
    {
        private readonly AuraService _auras;

        public ControlService(AuraService auras) => _auras = auras;

        public bool IsRooted(ulong sid)   => HasCategory(sid, AuraCategory.Root);
        public bool IsStunned(ulong sid)  => HasCategory(sid, AuraCategory.Stun);
        public bool IsSilenced(ulong sid) => HasCategory(sid, AuraCategory.Silence);

        /// Мультипликатор скорости перемещения (0..1). 1 — без замедления.
        public double GetMoveSpeedMultiplier(ulong sid)
        {
            var all = _auras.GetAll(sid);
            if (all.Count == 0) return 1.0;

            var now = DateTime.UtcNow;
            double maxSlowPct = 0.0;

            for (int i = all.Count - 1; i >= 0; i--)
            {
                var a = all[i];
                if (a.Until <= now) continue;
                if ((a.Categories & AuraCategory.Slow) == 0) continue;

                // трактуем Magnitude как проценты замедления (0..100)
                var pct = Math.Clamp(a.Magnitude, 0.0, 100.0);
                if (pct > maxSlowPct) maxSlowPct = pct;
            }

            var mult = 1.0 - maxSlowPct / 100.0;
            if (mult < 0) mult = 0;
            return mult;
        }

        private bool HasCategory(ulong sid, AuraCategory cat)
        {
            var all = _auras.GetAll(sid);
            if (all.Count == 0) return false;

            var now = DateTime.UtcNow;
            for (int i = all.Count - 1; i >= 0; i--)
            {
                var a = all[i];
                if (a.Until <= now) continue;
                if ((a.Categories & cat) != 0) return true;
            }
            return false;
        }
    }
}