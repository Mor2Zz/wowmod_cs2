namespace WarcraftCS2.Spells.Systems.Status
{
    /// Расширение для быстрого получения агрегированного состояния.
    public static class ControlServiceExtensions
    {
        public static MovementState GetMovementState(this ControlService control, ulong sid)
        {
            // нельзя двигаться, если root или stun
            bool canMove = !(control.IsRooted(sid) || control.IsStunned(sid));
            // нельзя кастовать, если stun или silence
            bool canCast = !(control.IsStunned(sid) || control.IsSilenced(sid));
            // учтём Slow (берём максимум из активных слоувов)
            double mult = control.GetMoveSpeedMultiplier(sid);

            return new MovementState(canMove, canCast, mult);
        }
    }
}