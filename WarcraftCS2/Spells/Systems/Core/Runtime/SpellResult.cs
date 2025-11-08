namespace WarcraftCS2.Spells.Systems
{
    public readonly struct SpellResult
    {
        public readonly bool Success;
        public readonly float ConsumedMana;
        public readonly float AppliedCooldown;
        public SpellResult(bool success, float consumedMana = 0, float appliedCooldown = 0)
        { Success = success; ConsumedMana = consumedMana; AppliedCooldown = appliedCooldown; }

        public static SpellResult Ok(float mana = 0, float cd = 0) => new SpellResult(true, mana, cd);
        public static SpellResult Fail() => new SpellResult(false, 0, 0);
    }
}