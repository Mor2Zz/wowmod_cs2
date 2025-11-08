namespace WarcraftCS2.Spells.Systems.Control.Break
{
    /// Активный контрол, который может сломаться от входящего урона.
    public readonly struct ActiveCc
    {
        public readonly ulong CasterSid;
        public readonly ulong TargetSid;
        public readonly int   SpellId;
        public readonly string Tag;
        public readonly float  Duration;       // сек (по факту — информативно)
        public readonly float  BreakFlat;      // порог по урону (если <= 0 — игнор)
        public readonly float  BreakPercent01; // если хочешь проценты — используйте свой провайдер правил

        public ActiveCc(ulong casterSid, ulong targetSid, int spellId, string tag, float duration, float breakFlat, float breakPercent01)
        {
            CasterSid = casterSid;
            TargetSid = targetSid;
            SpellId = spellId;
            Tag = tag;
            Duration = duration;
            BreakFlat = breakFlat;
            BreakPercent01 = breakPercent01;
        }
    }
}
