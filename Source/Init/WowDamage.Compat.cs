using System;
using System.Reflection;
using Microsoft.Extensions.Logging;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Damage;
using WarcraftCS2.Spells.Systems.Status;

namespace wowmod_cs2
{
    public partial class WowmodCs2
    {
        public bool WowShieldCompat(ulong targetSid, double absorbAmount, double durationSec, ulong sourceSid, AuraCategory category = default)
        {
            try
            {
                var t = typeof(ShieldService);
                var m = t.GetMethod("AddOrRefresh", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(double), typeof(double), typeof(ulong), typeof(AuraCategory) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, absorbAmount, durationSec, sourceSid, category }); return true; }
                m = t.GetMethod("AddOrRefresh", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(double), typeof(double), typeof(ulong) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, absorbAmount, durationSec, sourceSid }); return true; }
                m = t.GetMethod("AddOrRefresh", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(double), typeof(double), typeof(ulong) });
                if (m != null) { m.Invoke(null, new object[] { absorbAmount, durationSec, targetSid }); return true; }
            }
            catch (Exception ex) { Logger?.LogError(ex, "[wowmod] WowShieldCompat failed"); }
            return false;
        }

        public bool WowAddShieldCompat(ulong targetSid, double absorbAmount, double durationSec, ulong sourceSid)
            => WowShieldCompat(targetSid, absorbAmount, durationSec, sourceSid, default);

        public bool WowAddShieldCompat(ulong targetSid, double absorbAmount, double durationSec, ulong sourceSid, AuraCategory category)
            => WowShieldCompat(targetSid, absorbAmount, durationSec, sourceSid, category);

        public bool WowAddShieldCompat(ulong targetSid, string key, double absorbAmount, double durationSec, ulong sourceSid)
            => WowShieldCompat(targetSid, absorbAmount, durationSec, sourceSid, default);

        public bool WowAddShieldCompat(ulong targetSid, string key, double absorbAmount, double durationSec, ulong sourceSid, AuraCategory category)
            => WowShieldCompat(targetSid, absorbAmount, durationSec, sourceSid, category);

        public bool WowDispelCompat(ulong targetSid, int maxCount = 1, AuraCategory category = default)
        {
            try
            {
                var t = typeof(DispelService);
                var m = t.GetMethod("DispelNegative", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(int), typeof(AuraCategory) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, maxCount, category }); return true; }
                m = t.GetMethod("DispelNegative", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(int) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, maxCount }); return true; }
                m = t.GetMethod("DispelAll", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong) });
                if (m != null && maxCount <= 0) { m.Invoke(null, new object[] { targetSid }); return true; }
            }
            catch (Exception ex) { Logger?.LogError(ex, "[wowmod] WowDispelCompat failed"); }
            return false;
        }

        public bool WowTryDispelCompat(ulong targetSid)
            => WowDispelCompat(targetSid, 1, default);

        public bool WowTryDispelCompat(ulong targetSid, int maxCount)
            => WowDispelCompat(targetSid, maxCount, default);

        public bool WowTryDispelCompat(ulong targetSid, int maxCount, bool onlyBeneficialOnEnemy)
            => WowDispelCompat(targetSid, maxCount, default);

        public bool WowTryDispelCompat(ulong targetSid, int maxCount, bool onlyBeneficialOnEnemy, bool onlyHarmfulOnAlly)
            => WowDispelCompat(targetSid, maxCount, default);

        public bool WowTryDispelCompat(ulong targetSid, AuraCategory category)
            => WowDispelCompat(targetSid, 1, category);

        public bool WowTryDispelCompat(ulong targetSid, AuraCategory category, int maxCount)
            => WowDispelCompat(targetSid, maxCount, category);

        public bool WowTryDispelCompat(ulong targetSid, AuraCategory category, int maxCount, bool onlyBeneficialOnEnemy)
            => WowDispelCompat(targetSid, maxCount, category);

        public bool WowTryDispelCompat(ulong targetSid, AuraCategory category, int maxCount, bool onlyBeneficialOnEnemy, bool onlyHarmfulOnAlly)
            => WowDispelCompat(targetSid, maxCount, category);

        public bool WowAuraRemoveCompat(ulong targetSid, string auraKey)
        {
            try
            {
                var t = typeof(AuraService);
                var m = t.GetMethod("RemoveByKey", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(string) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, auraKey }); return true; }
                m = t.GetMethod("Remove", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(string) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, auraKey }); return true; }
            }
            catch (Exception ex) { Logger?.LogError(ex, "[wowmod] WowAuraRemoveCompat failed"); }
            return false;
        }

        public bool WowAddImmunityCompat(ulong targetSid, DamageSchool school, double durationSec, ulong sourceSid)
        {
            try
            {
                var dt = Type.GetType("WarcraftCS2.Spells.Systems.Damage.Services.DamageService")
                      ?? Type.GetType("WarcraftCS2.Spells.Systems.Damage.DamageService");
                var m = dt?.GetMethod("AddImmunity", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(DamageSchool), typeof(double), typeof(ulong) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, school, durationSec, sourceSid }); return true; }
                m = dt?.GetMethod("AddImmunity", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(DamageSchool), typeof(double) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, school, durationSec }); return true; }
                var at = typeof(AuraService);
                m = at.GetMethod("AddImmunity", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(DamageSchool), typeof(double), typeof(ulong) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, school, durationSec, sourceSid }); return true; }
                m = at.GetMethod("AddImmunity", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(DamageSchool), typeof(double) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, school, durationSec }); return true; }
            }
            catch (Exception ex) { Logger?.LogError(ex, "[wowmod] WowAddImmunityCompat failed"); }
            return false;
        }

        public bool WowRemoveImmunityCompat(ulong targetSid, DamageSchool school)
        {
            try
            {
                var dt = Type.GetType("WarcraftCS2.Spells.Systems.Damage.Services.DamageService")
                      ?? Type.GetType("WarcraftCS2.Spells.Systems.Damage.DamageService");
                var m = dt?.GetMethod("RemoveImmunity", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(DamageSchool) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, school }); return true; }
                var at = typeof(AuraService);
                m = at.GetMethod("RemoveImmunity", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong), typeof(DamageSchool) });
                if (m != null) { m.Invoke(null, new object[] { targetSid, school }); return true; }
            }
            catch (Exception ex) { Logger?.LogError(ex, "[wowmod] WowRemoveImmunityCompat failed"); }
            return false;
        }

        public bool WowAddAllMagicImmunityCompat(ulong targetSid, double durationSec, ulong sourceSid)
        {
            bool ok = false;
            try
            {
                foreach (DamageSchool s in Enum.GetValues(typeof(DamageSchool)))
                {
                    if (s == DamageSchool.Physical) continue;
                    ok |= WowAddImmunityCompat(targetSid, s, durationSec, sourceSid);
                }
            }
            catch (Exception ex) { Logger?.LogError(ex, "[wowmod] WowAddAllMagicImmunityCompat failed"); }
            return ok;
        }

        public bool WowAddAllMagicalImmunityCompat(ulong targetSid, double durationSec, ulong sourceSid)
            => WowAddAllMagicImmunityCompat(targetSid, durationSec, sourceSid);

        public bool WowRemoveAllMagicImmunityCompat(ulong targetSid)
        {
            bool ok = false;
            try
            {
                foreach (DamageSchool s in Enum.GetValues(typeof(DamageSchool)))
                {
                    if (s == DamageSchool.Physical) continue;
                    ok |= WowRemoveImmunityCompat(targetSid, s);
                }
            }
            catch (Exception ex) { Logger?.LogError(ex, "[wowmod] WowRemoveAllMagicImmunityCompat failed"); }
            return ok;
        }

        public bool WowAddAllSchoolsImmunityCompat(ulong targetSid, double durationSec, ulong sourceSid)
        {
            bool ok = false;
            foreach (DamageSchool s in Enum.GetValues(typeof(DamageSchool)))
                ok |= WowAddImmunityCompat(targetSid, s, durationSec, sourceSid);
            return ok;
        }

        public bool WowRemoveAllSchoolsImmunityCompat(ulong targetSid)
        {
            bool ok = false;
            foreach (DamageSchool s in Enum.GetValues(typeof(DamageSchool)))
                ok |= WowRemoveImmunityCompat(targetSid, s);
            return ok;
        }

        public bool WowAddPhysicalImmunityCompat(ulong targetSid, double durationSec, ulong sourceSid)
            => WowAddImmunityCompat(targetSid, DamageSchool.Physical, durationSec, sourceSid);

        public int WowTryRemoveAllShieldsCompat(ulong targetSid)
        {
            try
            {
                var t = typeof(ShieldService);
                var m = t.GetMethod("RemoveAll", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong) });
                if (m != null)
                {
                    var res = m.Invoke(null, new object[] { targetSid });
                    if (res is int i) return i;
                    return 1;
                }
                m = t.GetMethod("Remove", BindingFlags.Public | BindingFlags.Static, new Type[] { typeof(ulong) });
                if (m != null) { m.Invoke(null, new object[] { targetSid }); return 1; }
            }
            catch (Exception ex) { Logger?.LogError(ex, "[wowmod] WowTryRemoveAllShieldsCompat failed"); }
            return 0;
        }
    }
}