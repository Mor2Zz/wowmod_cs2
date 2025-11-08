using System;

namespace WarcraftCS2.Spells.Systems.Core.Targeting
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class TargetingAttribute : Attribute
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

        public TargetingPolicy ToPolicy() => new()
        {
            Kind = Kind,
            Shape = Shape,
            Range = Range,
            RequireLoS = RequireLoS,
            ExcludeSelf = ExcludeSelf,
            RequireAlive = RequireAlive,
            MaxTargets = MaxTargets,
            Radius = Radius,
            AngleDeg = AngleDeg
        };
    }
}
