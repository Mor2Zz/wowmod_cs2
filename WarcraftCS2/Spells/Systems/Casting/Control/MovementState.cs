namespace WarcraftCS2.Spells.Systems.Status
{
    /// Агрегированное состояние поведения игрока на основе аур (root/slow/stun/silence).
    public readonly struct MovementState
    {
        public readonly bool CanMove;          // false при root или stun
        public readonly bool CanCast;          // false при stun или silence
        public readonly double SpeedMultiplier; // 0..1 (учитывает Slow; 1 — без замедления)

        public MovementState(bool canMove, bool canCast, double speedMultiplier)
        {
            CanMove = canMove;
            CanCast = canCast;
            SpeedMultiplier = speedMultiplier < 0 ? 0 : (speedMultiplier > 1 ? 1 : speedMultiplier);
        }
    }
}