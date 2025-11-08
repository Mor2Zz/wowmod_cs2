namespace WarcraftCS2.Spells.Systems.Config
{
    public sealed class CombatConfig
    {
        public double GlobalGcdSec { get; set; } = 0.90;
        public double BaseMaxMana { get; set; } = 100.0;
        public double BaseManaRegenPerSec { get; set; } = 10.0;
    }
}