namespace WarcraftCS2.Spells.Systems.Core.Targeting
{
    public sealed class TargetingPolicy
    {
        public TargetKind Kind { get; set; } = TargetKind.Enemy;
        public ShapeKind Shape { get; set; } = ShapeKind.Single;

        public float Range { get; set; } = 750f;
        public bool RequireLoS { get; set; } = true;
        public bool ExcludeSelf { get; set; } = true;
        public bool RequireAlive { get; set; } = true;
        public int MaxTargets { get; set; } = 1;

        public float Radius { get; set; } = 200f;
        public float AngleDeg { get; set; } = 35f;

        public static TargetingPolicy EnemySingle(float range = 750, bool requireLoS = true) => new()
        { Kind = TargetKind.Enemy, Shape = ShapeKind.Single, Range = range, RequireLoS = requireLoS, MaxTargets = 1 };

        public static TargetingPolicy AllySingle(float range = 750, bool requireLoS = true) => new()
        { Kind = TargetKind.Ally, Shape = ShapeKind.Single, Range = range, RequireLoS = requireLoS, MaxTargets = 1 };

        public static TargetingPolicy SelfOnly() => new()
        { Kind = TargetKind.Self, Shape = ShapeKind.Single, Range = 0, RequireLoS = false, ExcludeSelf = false, MaxTargets = 1 };

        public static TargetingPolicy EnemyAoE(float range = 750, float radius = 250, int maxTargets = 5, bool requireLoS = true) => new()
        { Kind = TargetKind.Enemy, Shape = ShapeKind.AoE, Range = range, Radius = radius, MaxTargets = maxTargets, RequireLoS = requireLoS };

        public static TargetingPolicy EnemyCone(float range = 750, float angleDeg = 35, int maxTargets = 3, bool requireLoS = true) => new()
        { Kind = TargetKind.Enemy, Shape = ShapeKind.Cone, Range = range, AngleDeg = angleDeg, MaxTargets = maxTargets, RequireLoS = requireLoS };
    }
}