using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Направленные шаблоны: конус и луч (дамаг/хил + канальные варианты).
    public static class Directional
    {
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public enum HitFilter { Enemies, Allies, All }

        private static bool PassesFilter(ISpellRuntime rt, in TargetSnapshot caster, in TargetSnapshot t, HitFilter f)
            => f switch
            {
                HitFilter.Enemies => rt.IsEnemy(caster, t),
                HitFilter.Allies  => rt.IsAlly(caster, t),
                _                 => true
            };

        // ==========================
        // КОНУС – МГНОВЕННЫЙ УРОН
        // ==========================
        public sealed class ConeDamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  Amount = 30f;

            public float  Range = 10f;
            public float  AngleDeg = 60f;
            public bool   Flat = true;

            public HitFilter Filter = HitFilter.Enemies;

            // LoS (по умолчанию выключен — поведение как раньше)
            public bool   RequireLoS = true;
            public bool   WorldOnly  = false;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult ConeDamage(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            ConeDamageConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            Vector3 origin = caster.Position;
            Vector3 dir    = targetRef.Position - origin;
            if (cfg.Flat) dir.Z = 0f;
            if (dir.LengthSquared() < 1e-6f) dir = caster.Forward;
            if (cfg.Flat) dir.Z = 0f;
            if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
            dir = Vector3.Normalize(dir);

            float maxDist2 = cfg.Range * cfg.Range;
            float halfRad  = MathF.Max(0.001f, MathF.PI * cfg.AngleDeg / 360f);
            float cosHalf  = MathF.Cos(halfRad);

            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                if (!rt.IsAlive(t)) continue;
                if (!PassesFilter(rt, caster, t, cfg.Filter)) continue;

                Vector3 v = t.Position - origin;
                if (cfg.Flat) v.Z = 0f;

                float d2 = v.LengthSquared();
                if (d2 > maxDist2 || d2 < 1e-6f) continue;

                var vn = v / MathF.Sqrt(d2);
                float dot = Vector3.Dot(dir, vn);
                if (dot < cosHalf) continue;

                // LoS (если включён)
                if (cfg.RequireLoS)
                {
                    bool ok = cfg.WorldOnly
                        ? LineOfSight.VisibleWorldOnly(caster, t)
                        : LineOfSight.Visible(caster, t);
                    if (!ok) continue;
                }

                int tsid = rt.SidOf(t);
                if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;

                float resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                float dmg = MathF.Max(0f, cfg.Amount * (1f - resist));
                if (dmg <= 0f) continue;

                rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, cfg.School));
            }

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ==========================
        // ЛУЧ – МГНОВЕННЫЙ УРОН
        // ==========================
        public sealed class BeamDamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  Amount = 35f;

            public float  Range = 20f;
            public float  Radius = 0.6f;
            public bool   Flat = true;

            public HitFilter Filter = HitFilter.Enemies;

            // LoS
            public bool   RequireLoS = true;
            public bool   WorldOnly  = false;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFx; public string? PlaySfx;
        }

        private static bool InCylinder(Vector3 origin, Vector3 dirNorm, float range, float radius, Vector3 point, bool flat)
        {
            if (flat)
            {
                origin.Z = 0f; dirNorm.Z = 0f; point.Z = 0f;
                if (dirNorm.LengthSquared() < 1e-6f) dirNorm = Vector3.UnitX;
                dirNorm = Vector3.Normalize(dirNorm);
            }

            Vector3 op = point - origin;
            float t = Vector3.Dot(op, dirNorm);
            if (t < 0f || t > range) return false;

            Vector3 proj = origin + dirNorm * t;
            Vector3 perp = point - proj;
            return perp.LengthSquared() <= radius * radius;
        }

        public static SpellResult BeamDamage(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            BeamDamageConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            Vector3 origin = caster.Position;
            Vector3 dir    = targetRef.Position - origin;
            if (cfg.Flat) dir.Z = 0f;
            if (dir.LengthSquared() < 1e-6f) dir = caster.Forward;
            if (cfg.Flat) dir.Z = 0f;
            if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
            dir = Vector3.Normalize(dir);

            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                if (!rt.IsAlive(t)) continue;
                if (!PassesFilter(rt, caster, t, cfg.Filter)) continue;

                if (!InCylinder(origin, dir, cfg.Range, cfg.Radius, t.Position, cfg.Flat)) continue;

                // LoS (если включён)
                if (cfg.RequireLoS)
                {
                    bool ok = cfg.WorldOnly
                        ? LineOfSight.VisibleWorldOnly(caster, t)
                        : LineOfSight.Visible(caster, t);
                    if (!ok) continue;
                }

                int tsid = rt.SidOf(t);
                if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;

                float resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                float dmg = MathF.Max(0f, cfg.Amount * (1f - resist));
                if (dmg <= 0f) continue;

                rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, cfg.School));
            }

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ==========================
        // КОНУС – МГНОВЕННЫЙ ХИЛ
        // ==========================
        public sealed class ConeHealConfig
        {
            public int   SpellId;
            public float Amount = 35f;

            public float Range = 10f;
            public float AngleDeg = 60f;
            public bool  Flat = true;

            public bool  IncludeSelf = true;

            public float Mana = 0f;
            public float Gcd  = 0f;
            public float Cooldown = 0f;

            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult ConeHeal(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            ConeHealConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            Vector3 origin = caster.Position;
            Vector3 dir    = targetRef.Position - origin;
            if (cfg.Flat) dir.Z = 0f;
            if (dir.LengthSquared() < 1e-6f) dir = caster.Forward;
            if (cfg.Flat) dir.Z = 0f;
            if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
            dir = Vector3.Normalize(dir);

            float maxDist2 = cfg.Range * cfg.Range;
            float halfRad  = MathF.Max(0.001f, MathF.PI * cfg.AngleDeg / 360f);
            float cosHalf  = MathF.Cos(halfRad);

            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                if (!rt.IsAlive(t)) continue;
                if (!rt.IsAlly(caster, t)) continue;
                if (!cfg.IncludeSelf && rt.SidOf(t) == csid) continue;

                Vector3 v = t.Position - origin;
                if (cfg.Flat) v.Z = 0f;

                float d2 = v.LengthSquared();
                if (d2 > maxDist2 || d2 < 1e-6f) continue;

                var vn = v / MathF.Sqrt(d2);
                float dot = Vector3.Dot(dir, vn);
                if (dot < cosHalf) continue;

                int tsid = rt.SidOf(t);
                rt.Heal(csid, tsid, cfg.SpellId, MathF.Max(0f, cfg.Amount));
            }

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ==========================
        // ЛУЧ – МГНОВЕННЫЙ ХИЛ
        // ==========================
        public sealed class BeamHealConfig
        {
            public int   SpellId;
            public float Amount = 40f;

            public float Range = 20f;
            public float Radius = 0.6f;
            public bool  Flat = true;

            public bool  IncludeSelf = true;

            public float Mana = 0f;
            public float Gcd  = 0f;
            public float Cooldown = 0f;

            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult BeamHeal(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            BeamHealConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            Vector3 origin = caster.Position;
            Vector3 dir    = targetRef.Position - origin;
            if (cfg.Flat) dir.Z = 0f;
            if (dir.LengthSquared() < 1e-6f) dir = caster.Forward;
            if (cfg.Flat) dir.Z = 0f;
            if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
            dir = Vector3.Normalize(dir);

            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                if (!rt.IsAlive(t)) continue;
                if (!rt.IsAlly(caster, t)) continue;
                if (!cfg.IncludeSelf && rt.SidOf(t) == csid) continue;

                if (!InCylinder(origin, dir, cfg.Range, cfg.Radius, t.Position, cfg.Flat)) continue;

                int tsid = rt.SidOf(t);
                rt.Heal(csid, tsid, cfg.SpellId, MathF.Max(0f, cfg.Amount));
            }

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ==========================
        // КАНАЛЬНЫЙ КОНУС – ХИЛ
        // ==========================
        public sealed class ChannelConeHealConfig
        {
            public int   SpellId;
            public float TickAmount = 12f;
            public float Duration   = 3f;
            public float TickEvery  = 0.25f;

            public float Range = 10f;
            public float AngleDeg = 60f;
            public bool  Flat = true;

            public bool  IncludeSelf = true;

            public float Mana = 0f;
            public float Gcd  = 0f;
            public float Cooldown = 0f;

            public Func<bool>? ExtraCancel;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxTick;  public string? PlaySfxTick;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult ChannelConeHeal(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            ChannelConeHealConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            Func<bool> cancel = () => cfg.ExtraCancel != null && cfg.ExtraCancel();

            float tick = MathF.Max(0.05f, cfg.TickEvery);
            float halfRad = MathF.Max(0.001f, MathF.PI * cfg.AngleDeg / 360f);
            float cosHalf = MathF.Cos(halfRad);

            rt.StartChannel(
                csid, cfg.SpellId, MathF.Max(0.05f, cfg.Duration), tick,
                isCancelled: cancel,
                onTick: () =>
                {
                    Vector3 origin = caster.Position;
                    Vector3 dir    = targetRef.Position - origin;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = caster.Forward;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
                    dir = Vector3.Normalize(dir);

                    float maxDist2 = cfg.Range * cfg.Range;

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!rt.IsAlly(caster, t)) continue;
                        if (!cfg.IncludeSelf && rt.SidOf(t) == csid) continue;

                        Vector3 v = t.Position - origin;
                        if (cfg.Flat) v.Z = 0f;

                        float d2 = v.LengthSquared();
                        if (d2 > maxDist2 || d2 < 1e-6f) continue;

                        var vn = v / MathF.Sqrt(d2);
                        float dot = Vector3.Dot(dir, vn);
                        if (dot < cosHalf) continue;

                        int tsid = rt.SidOf(t);
                        rt.Heal(csid, tsid, cfg.SpellId, MathF.Max(0f, cfg.TickAmount));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, caster);
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, caster);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ==========================
        // КАНАЛЬНЫЙ ЛУЧ – ХИЛ
        // ==========================
        public sealed class ChannelBeamHealConfig
        {
            public int   SpellId;
            public float TickAmount = 14f;
            public float Duration   = 3f;
            public float TickEvery  = 0.25f;

            public float Range = 20f;
            public float Radius = 0.6f;
            public bool  Flat = true;

            public bool  IncludeSelf = true;

            public float Mana = 0f;
            public float Gcd  = 0f;
            public float Cooldown = 0f;

            public Func<bool>? ExtraCancel;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxTick;  public string? PlaySfxTick;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult ChannelBeamHeal(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            ChannelBeamHealConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            Func<bool> cancel = () => cfg.ExtraCancel != null && cfg.ExtraCancel();

            float tick = MathF.Max(0.05f, cfg.TickEvery);

            rt.StartChannel(
                csid, cfg.SpellId, MathF.Max(0.05f, cfg.Duration), tick,
                isCancelled: cancel,
                onTick: () =>
                {
                    Vector3 origin = caster.Position;
                    Vector3 dir    = targetRef.Position - origin;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = caster.Forward;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
                    dir = Vector3.Normalize(dir);

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!rt.IsAlly(caster, t)) continue;
                        if (!cfg.IncludeSelf && rt.SidOf(t) == csid) continue;

                        if (!InCylinder(origin, dir, cfg.Range, cfg.Radius, t.Position, cfg.Flat)) continue;

                        int tsid = rt.SidOf(t);
                        rt.Heal(csid, tsid, cfg.SpellId, MathF.Max(0f, cfg.TickAmount));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, caster);
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, caster);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ==========================
        // КАНАЛЬНЫЙ КОНУС – УРОН
        // ==========================
        public sealed class ChannelConeDamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  TickAmount = 10f;
            public float  Duration   = 3f;
            public float  TickEvery  = 0.25f;

            public float  Range = 10f;
            public float  AngleDeg = 60f;
            public bool   Flat = true;

            // LoS
            public bool   RequireLoS = true;
            public bool   WorldOnly  = false;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public Func<bool>? ExtraCancel;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxTick;  public string? PlaySfxTick;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult ChannelConeDamage(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            ChannelConeDamageConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            Func<bool> cancel = () => cfg.ExtraCancel != null && cfg.ExtraCancel();

            float tick = MathF.Max(0.05f, cfg.TickEvery);
            float halfRad = MathF.Max(0.001f, MathF.PI * cfg.AngleDeg / 360f);
            float cosHalf = MathF.Cos(halfRad);

            rt.StartChannel(
                csid, cfg.SpellId, MathF.Max(0.05f, cfg.Duration), tick,
                isCancelled: cancel,
                onTick: () =>
                {
                    Vector3 origin = caster.Position;
                    Vector3 dir    = targetRef.Position - origin;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = caster.Forward;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
                    dir = Vector3.Normalize(dir);

                    float maxDist2 = cfg.Range * cfg.Range;

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!rt.IsEnemy(caster, t)) continue;

                        Vector3 v = t.Position - origin;
                        if (cfg.Flat) v.Z = 0f;

                        float d2 = v.LengthSquared();
                        if (d2 > maxDist2 || d2 < 1e-6f) continue;

                        var vn = v / MathF.Sqrt(d2);
                        float dot = Vector3.Dot(dir, vn);
                        if (dot < cosHalf) continue;

                        // LoS (если включён)
                        if (cfg.RequireLoS)
                        {
                            bool ok = cfg.WorldOnly
                                ? LineOfSight.VisibleWorldOnly(caster, t)
                                : LineOfSight.Visible(caster, t);
                            if (!ok) continue;
                        }

                        int tsid = rt.SidOf(t);
                        if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;

                        float resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                        float dmg = MathF.Max(0f, cfg.TickAmount * (1f - resist));
                        if (dmg <= 0f) continue;

                        rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                        ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, cfg.School));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, caster);
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, caster);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ==========================
        // КАНАЛЬНЫЙ ЛУЧ – УРОН
        // ==========================
        public sealed class ChannelBeamDamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  TickAmount = 12f;
            public float  Duration   = 3f;
            public float  TickEvery  = 0.25f;

            public float  Range = 20f;
            public float  Radius = 0.6f;
            public bool   Flat = true;

            // LoS
            public bool   RequireLoS = true;
            public bool   WorldOnly  = false;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public Func<bool>? ExtraCancel;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxTick;  public string? PlaySfxTick;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult ChannelBeamDamage(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            ChannelBeamDamageConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);

            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            Func<bool> cancel = () => cfg.ExtraCancel != null && cfg.ExtraCancel();

            float tick = MathF.Max(0.05f, cfg.TickEvery);

            rt.StartChannel(
                csid, cfg.SpellId, MathF.Max(0.05f, cfg.Duration), tick,
                isCancelled: cancel,
                onTick: () =>
                {
                    Vector3 origin = caster.Position;
                    Vector3 dir    = targetRef.Position - origin;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = caster.Forward;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
                    dir = Vector3.Normalize(dir);

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!rt.IsEnemy(caster, t)) continue;

                        if (!InCylinder(origin, dir, cfg.Range, cfg.Radius, t.Position, cfg.Flat)) continue;

                        // LoS (если включён)
                        if (cfg.RequireLoS)
                        {
                            bool ok = cfg.WorldOnly
                                ? LineOfSight.VisibleWorldOnly(caster, t)
                                : LineOfSight.Visible(caster, t);
                            if (!ok) continue;
                        }

                        int tsid = rt.SidOf(t);
                        if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;

                        float resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                        float dmg = MathF.Max(0f, cfg.TickAmount * (1f - resist));
                        if (dmg <= 0f) continue;

                        rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                        ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, cfg.School));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, caster);
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, caster);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}