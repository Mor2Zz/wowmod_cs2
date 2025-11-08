using System;

namespace RPG.XP
{
    /// Атрибут для указания роли прямо в классе (Dps / Support / Tank)
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class WowRoleAttribute : Attribute
    {
        public PlayerRole Role { get; }
        public WowRoleAttribute(PlayerRole role) => Role = role;
    }
}