using System;
using WarcraftCS2.Spells.Systems.Damage.Services; 

namespace WarcraftCS2.Spells.Systems.Status
{
    public enum AuraRefreshMode
    {
        /// Сбросить таймер и ограничить стаки сверху (по MaxStacks).
        RefreshDuration_AddStackCap,
        /// Только обновить длительность, стаки не изменять.
        RefreshDuration_KeepStacks,
        /// Только стаки (до MaxStacks), длительность не трогать.
        AddStack_KeepDuration
    }

    public sealed class AuraState
    {
        public string AuraId { get; set; } = "";
        public AuraCategory Categories { get; set; } = AuraCategory.None;
        public ulong SourceSid { get; set; }
        public int Stacks { get; set; } = 1;
        public int MaxStacks { get; set; } = 1;
        public DateTime Until { get; set; } = DateTime.UtcNow;

        /// Сила/величина эффекта. Например, для Slow — проценты замедления (0..100).
        /// Для других аур можно трактовать по-своему. По умолчанию 0.
        public double Magnitude { get; set; } = 0.0;
    }
}