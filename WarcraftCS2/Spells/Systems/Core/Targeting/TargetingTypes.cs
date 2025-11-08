using System.Numerics;

namespace WarcraftCS2.Spells.Systems.Core.Targeting
{
    public enum TargetKind { Self, Ally, Enemy, Any }
    public enum ShapeKind { Single, AoE, Cone }

    public struct TargetSnapshot
    {
        public ulong SteamId;
        public Vector3 Position;
        public Vector3 Forward;
        public int Team;
        public bool Alive;
        public bool IsSelf;
    }
}