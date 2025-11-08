using System;
using WarcraftCS2.Spells.Systems.Damage;

namespace WarcraftCS2.Spells.Systems.Status.Periodic
{
    public enum PeriodicKind
    {
        DoT, // damage over time
        HoT  // heal over time
    }

    public sealed class PeriodicEntry
    {
        public string Id { get; set; } = "";           // уникальный ID эффекта на цели (например, "warlock_corruption")
        public PeriodicKind Kind { get; set; }          // DoT или HoT
        public ulong CasterSid { get; set; }            // кто наложил
        public ulong TargetSid { get; set; }            // на ком висит
        public DamageSchool School { get; set; }        // школа урона (для DoT)
        public double AmountPerTick { get; set; }       // сколько наносим/лечим за тик
        public double IntervalSec { get; set; }         // период тика (обычно 1.0)
        public DateTime NextTickUtc { get; set; }       // когда тикать в следующий раз
        public DateTime UntilUtc { get; set; }          // когда истекает эффект
        public int MaxStacks { get; set; } = 1;         // максимум стаков (если надо)
        public int Stacks { get; set; } = 1;            // текущие стаки
    }
}