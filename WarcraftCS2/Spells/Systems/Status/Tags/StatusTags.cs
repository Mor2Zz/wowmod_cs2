using System;

namespace WarcraftCS2.Spells.Systems.Status;

    public enum DamageKind { Physical, Magic, True }

    [Flags]
    public enum StatusTag
    {
        None           = 0,
        // Иммунитеты
        ImmuneAll      = 1 << 0,
        ImmunePhysical = 1 << 1,
        ImmuneMagic    = 1 << 2,

        // Модификаторы входящего урона
        ReduceDamage   = 1 << 3,   // множитель < 1
        BonusDamage    = 1 << 4,   // множитель > 1
        CapDamage      = 1 << 5,   // ограничение максимального урона (после множителей)

        // Влияние на спелл-систему
        Haste          = 1 << 6,   // быстрее кд: множитель < 1
        Slow           = 1 << 7,   // медленнее кд: множитель > 1
        Silence        = 1 << 8,   // запрет кастов активок
        Root           = 1 << 9,   // рут (на будущее)
    }