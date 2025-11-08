using System;

namespace WarcraftCS2.Spells.Systems.Status;

/// Утилиты модификации входящего урона на основе статусов:
/// вычисление итогового множителя и кэпа после множителей.
public static class DamageModifiers
{
    /// Возвращает множитель урона (0..∞) и кэп после множителей (если есть).
    /// Порядок внутри StatusStore: иммунитеты-теги → редукции → бонусы → кэп.
    public static (double multiplier, double? capAfter) ComputeFor(ulong victimSteamId, DamageKind kind)
        => StatusStore.IncomingDamageFor(victimSteamId, kind);

    /// Применяет множители/кэп к числу урона. Если хочешь сам считать пайп — используй это.
    public static double Apply(double baseDamage, ulong victimSteamId, DamageKind kind)
    {
        var (mul, cap) = ComputeFor(victimSteamId, kind);
        if (mul <= 0) return 0;
        var val = baseDamage * mul;
        if (cap is not null && val > cap.Value) val = cap.Value;
        return Math.Max(0, val);
    }
}