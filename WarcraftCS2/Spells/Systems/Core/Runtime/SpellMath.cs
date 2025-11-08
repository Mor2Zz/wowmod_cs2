using System;

namespace WarcraftCS2.Spells.Systems.Core.Runtime
{
    /// Вспомогательная математика для спеллов: криты, клампы, falloff и пр.
    public static class SpellMath
    {
        /// Возвращает true, если крит сработал при вероятности [0..1].
        public static bool RollCrit(Random rng, float critChance01)
        => rng.NextDouble() < Clamp01(critChance01);


        public static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
        public static float Clamp(float v, float min, float max)
        => v < min ? min : (v > max ? max : v);


        /// Линейный falloff от 1 на d=0 до 0 на d>=max.
        public static float FalloffLinear(float distance, float maxDistance)
        {
            if (maxDistance <= 0) return 0;
            var t = 1f - (distance / maxDistance);
            return t < 0 ? 0 : t;
        }


        /// Мягкий спад (~1/sqrt), на 0 дистанции = 1.
        public static float FalloffInverseSqrt(float distance, float scale)
        {
            if (scale <= 0) return 0;
            return 1f / MathF.Sqrt(1f + (distance / scale));
        }


        /// Экспоненциальный спад для chain (например 0.85^hop).
        public static float ChainDecay(int hopIndex, float decayPerHop01)
        {
            var d = Clamp01(decayPerHop01);
            return MathF.Pow(1f - d, MathF.Max(0, hopIndex));
        }
    }
}