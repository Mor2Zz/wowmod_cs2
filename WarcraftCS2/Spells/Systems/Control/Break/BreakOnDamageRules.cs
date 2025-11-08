using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Control.Break
{
    /// Правила «break on damage» для контроля (fear/poly/чары и т.п.).
    /// Отдельно от пайплайна урона и аур — чистая логика принятия решения.
    public static class BreakOnDamageRules
    {
        /// Возвращает true, если контроль должен сорваться от нанесённого урона.

        /// <param name="damage">реальный нанесённый урон (после щитов/резистов)</param>
        /// <param name="victimMaxHp">максимальное HP цели (для порога в процентах)</param>
        /// <param name="flatThreshold">порог в абсолюте, при котором точно ломаем (например, 5)</param>
        /// <param name="percentThreshold01">порог в долях от maxHP (например, 0.1 = 10%)</param>
        public static bool ShouldBreak(float damage, float victimMaxHp, float flatThreshold, float percentThreshold01)
        {
            if (damage <= 0) return false;
            if (damage >= flatThreshold) return true;
            if (victimMaxHp > 1e-3f && (damage / victimMaxHp) >= percentThreshold01) return true;
            return false;
        }
    }

    /// Контракт для пользовательских правил брейка. Если поставишь свой провайдер — сервис будет звать его.
    public interface IBreakOnDamageRule
    {
        /// true — контроль нужно снять<
        bool ShouldBreak(in ProcBus.DamageArgs hit, in ActiveCc cc, ISpellRuntime rt);
    }
}