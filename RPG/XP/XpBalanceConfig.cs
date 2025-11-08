namespace RPG.XP;

public sealed class XpBalanceConfig
{
    public GainsConfig Gains { get; set; } = new();
    public LevelScalingConfig LevelScaling { get; set; } = new();
    public AntiFarmConfig AntiFarm { get; set; } = new();
    public RolesConfig Roles { get; set; } = new();

    public sealed class GainsConfig
    {
        // === БОЕВЫЕ БАЗЫ (как в wowmod-master) ===
        public double KillBase { get; set; } = 50;
        public double HeadshotBonus { get; set; } = 25;
        public double AssistBase { get; set; } = 25;

        // === УРОН / ЛЕЧЕНИЕ → XP ===
        public double DamagePer100 { get; set; } = 0; // было 10
        public double HealPer100   { get; set; } = 0; // было 8
    }

    public sealed class LevelScalingConfig
    {
        // Мягкий бонус/штраф за разницу уровней
        public bool Enabled { get; set; } = true;
        public int MaxBonusAtDiff { get; set; } = 8;            // victim ≫ attacker → бонус
        public double MaxBonusMultiplier { get; set; } = 1.5;   // → до ×1.5
        public int MaxPenaltyAtDiff { get; set; } = 8;          // attacker ≫ victim → штраф
        public double MaxPenaltyMultiplier { get; set; } = 0.5; // → до ×0.5
    }

    public sealed class AntiFarmConfig
    {
        // Окна/капы/спад множителя A→B (и для урона, и для хила)
        public int DamageWindowSec { get; set; } = 20;
        public int DamageSoftCap   { get; set; } = 6;
        public double DamageDecayPerEvent { get; set; } = 0.10;
        public double DamageMinFactor     { get; set; } = 0.25;

        public int HealWindowSec { get; set; } = 20;
        public int HealSoftCap   { get; set; } = 6;
        public double HealDecayPerEvent { get; set; } = 0.10;
        public double HealMinFactor     { get; set; } = 0.25;
    }

    public sealed class RolesConfig
    {
        public bool Enabled { get; set; } = true;
        public double DpsMultiplier     { get; set; } = 1.00;
        public double SupportMultiplier { get; set; } = 1.10;
        public double TankMultiplier    { get; set; } = 0.95;
    }
}