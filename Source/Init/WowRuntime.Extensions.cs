using System;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Damage.Services;

namespace wowmod_cs2
{
    public static class WowRuntimeExtensions
    {
        public static bool WowTryDispelCompat(this IWowRuntime runtime, ulong targetSid, AuraCategory category)
        {
            if (runtime is WowmodCs2 p) return p.WowDispelCompat(targetSid, 1, category);
            return false;
        }

        public static bool WowTryDispelCompat(this IWowRuntime runtime, ulong targetSid, AuraCategory category, int maxCount)
        {
            if (runtime is WowmodCs2 p) return p.WowDispelCompat(targetSid, maxCount, category);
            return false;
        }

        public static bool WowTryDispelCompat(this IWowRuntime runtime, ulong targetSid, AuraCategory category, int maxCount, bool onlyBeneficialOnEnemy)
        {
            if (runtime is WowmodCs2 p) return p.WowDispelCompat(targetSid, maxCount, category);
            return false;
        }

        public static bool WowTryDispelCompat(this IWowRuntime runtime, ulong targetSid, AuraCategory category, int maxCount, bool onlyBeneficialOnEnemy, bool onlyHarmfulOnAlly)
        {
            if (runtime is WowmodCs2 p) return p.WowDispelCompat(targetSid, maxCount, category);
            return false;
        }

        public static bool WowAddShieldCompat(this IWowRuntime runtime, ulong targetSid, string key, double absorbAmount, double durationSec, ulong sourceSid)
        {
            if (runtime is WowmodCs2 p) return p.WowAddShieldCompat(targetSid, key, absorbAmount, durationSec, sourceSid);
            return false;
        }

        public static bool WowAddShieldCompat(this IWowRuntime runtime, ulong targetSid, string key, double absorbAmount, double durationSec, ulong sourceSid, AuraCategory category)
        {
            if (runtime is WowmodCs2 p) return p.WowAddShieldCompat(targetSid, key, absorbAmount, durationSec, sourceSid, category);
            return false;
        }
    }
}