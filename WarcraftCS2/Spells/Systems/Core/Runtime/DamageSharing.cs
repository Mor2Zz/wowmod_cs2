using System;
using System.Collections.Generic;
using System.Numerics;


namespace WarcraftCS2.Spells.Systems.Core.Runtime
{
    /// Распределение урона по списку целей (AoE/Chain) с поддержкой разных законов спада.
    /// Не применяет урон — только считает множители.
    public static class DamageSharing
    {
        /// /// Равномерно всем.
        public static float[] Uniform(int targetCount, float totalDamage)
        {
            if (targetCount <= 0) return Array.Empty<float>();
            var each = totalDamage / targetCount;
            var arr = new float[targetCount];
            for (int i = 0; i < targetCount; i++) arr[i] = each;
            return arr;
        }

        /// Конус/радиус: урон с линейным спадом от центра (origin) до maxRange.
        /// positions[i] — позиция цели i; веса нормируются на totalDamage.
        public static float[] RadialLinear(in Vector3 origin, IReadOnlyList<Vector3> positions, float maxRange, float totalDamage)
        {
            var n = positions.Count;
            if (n == 0 || maxRange <= 0) return Array.Empty<float>();
            var weights = new float[n];
            float sum = 0;
            for (int i = 0; i < n; i++)
            {
                var d = Vector3.Distance(origin, positions[i]);
                var w = SpellMath.FalloffLinear(d, maxRange);
                weights[i] = w; sum += w;
            }
            if (sum <= 1e-6f) return Uniform(n, totalDamage);
            var res = new float[n];
            for (int i = 0; i < n; i++) res[i] = totalDamage * (weights[i] / sum);
            return res;
        }

        /// Цепочка: экспо-спад по прыжкам (0-й — seed), totalDamage распределяется по hops.
        public static float[] ChainByHop(int targetCount, float totalDamage, float decayPerHop01)
        {
            if (targetCount <= 0) return Array.Empty<float>();
            if (targetCount == 1) return new[] { totalDamage };
            var weights = new float[targetCount];
            float sum = 0;
            for (int i = 0; i < targetCount; i++)
            {
                var w = SpellMath.ChainDecay(i, decayPerHop01);
                weights[i] = w; sum += w;
            }
            var res = new float[targetCount];
            for (int i = 0; i < targetCount; i++) res[i] = totalDamage * (weights[i] / sum);
            return res;
        }
    }
}