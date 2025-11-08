using System;
using System.Collections.Generic;
using System.Reflection;

namespace WarcraftCS2.Spells.Systems.Core.Targeting
{
    public static class TargetingGate
    {
        public static bool TryResolveTargets(
            object spellInstance,
            TargetSnapshot caster,
            IReadOnlyList<TargetSnapshot> candidates,
            Func<int, int, bool> areAllies,
            out List<TargetSnapshot> targets,
            out string failReason,
            Func<TargetSnapshot, TargetSnapshot, bool>? hasLoS = null,
            TargetingPolicy? fallbackPolicy = null)
        {
            var policy = GetPolicyFromAttribute(spellInstance.GetType())
                         ?? GetStaticPolicyField(spellInstance.GetType())
                         ?? fallbackPolicy
                         ?? TargetingPolicy.EnemySingle();

            var service = new TargetingService();
            var (ok, list, reason) = service.ResolveTargets(caster, candidates, policy, areAllies, hasLoS);
            targets = list;
            failReason = reason;
            return ok;
        }

        private static TargetingPolicy? GetPolicyFromAttribute(Type t)
            => t.GetCustomAttribute<TargetingAttribute>(inherit: false)?.ToPolicy();

        private static TargetingPolicy? GetStaticPolicyField(Type t)
        {
            var f = t.GetField("Policy", BindingFlags.Public | BindingFlags.Static);
            if (f?.FieldType == typeof(TargetingPolicy)) return (TargetingPolicy?)f.GetValue(null);
            return null;
        }
    }
}