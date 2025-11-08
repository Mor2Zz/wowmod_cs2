using System;
using WarcraftCS2.Spells.Systems.Damage.Services; 

namespace WarcraftCS2.Spells.Systems.Status
{
    /// Группы DR. Slow не участвует, Silence участвует.
    public enum DRGroup
    {
        Stun,
        Root,
        Silence
    }

    public static class DiminishingReturnsConfig
    {
        /// Окно DR: все применения внутри этого окна накапливают стадию.
        public const double WindowSeconds = 15.0;

        /// Мультипликаторы по стадиям: 0 -> 100%, 1 -> 50%, 2 -> 25%, 3+ -> иммун (0%).
        public static readonly double[] Multipliers = { 1.0, 0.5, 0.25, 0.0 };

        public static bool TryMap(AuraCategory cat, out DRGroup group)
        {
            // при наличии нескольких флагов — приоритизируем «жёстче»
            if ((cat & AuraCategory.Stun) != 0)    { group = DRGroup.Stun;    return true; }
            if ((cat & AuraCategory.Root) != 0)    { group = DRGroup.Root;    return true; }
            if ((cat & AuraCategory.Silence) != 0) { group = DRGroup.Silence; return true; }
            group = default;
            return false;
        }
    }
}