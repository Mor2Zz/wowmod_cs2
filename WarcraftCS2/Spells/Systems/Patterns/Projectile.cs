using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Снаряды: линейные и самонаводящиеся. Шаговое обновление через StartPeriodic.
    public static class Projectile
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

        // ------------------------------------------------------------
        // ОБЩИЕ КОНФИГИ ДЛЯ ЭФФЕКТОВ ПРИ ПОПАДАНИИ
        // ------------------------------------------------------------
        public sealed class ImpactAuraConfig
        {
            public string Tag = "debuff";
            public float  Magnitude = 1f;
            public float  Duration = 2f;
        }

        public sealed class ImpactAuraAoEConfig
        {
            public string    Tag = "debuff";
            public float     Magnitude = 1f;
            public float     Duration = 2f;
            public float     Radius = 3f;
            public HitFilter Filter = HitFilter.Enemies;
            public string?   PlayFx; public string? PlaySfx;
        }

        public sealed class ImpactAuraChainConfig
        {
            public string    Tag = "debuff";
            public float     MagnitudeStart = 1f;
            public float     MagnitudeDecayPerHop = 0f; // 0..1
            public float     MinMagnitude = 0.01f;
            public float     Duration = 2f;

            public int       MaxJumps = 3;
            public float     JumpRadius = 6f;
            public float     JumpDelay  = 0.1f;
            public HitFilter Filter = HitFilter.Enemies;

            public string?   PlayFxHop; public string? PlaySfxHop;
        }

        public sealed class ImpactKnockbackConfig
        {
            public float Distance   = 3f;
            public float ApexHeight = 0.8f;
            public int   Steps      = 8;
            public float TickEvery  = 0.05f;
            public bool  Flat       = true;
        }

        public sealed class ImpactExplosionConfig
        {
            public float     Radius      = 3f;
            public string    School      = "magic";
            public float     Amount      = 20f;
            public HitFilter Filter      = HitFilter.Enemies;
            public string?   RequireNoImmunityTag;
            public string?   PlayFx; public string? PlaySfx;
        }

        public sealed class ImpactOptions
        {
            public ImpactAuraConfig?      Aura;
            public ImpactAuraAoEConfig?   AuraAoE;
            public ImpactAuraChainConfig? AuraChain;
            public ImpactKnockbackConfig? Knockback;
            public ImpactExplosionConfig? Explosion;
        }

        // ------------------------------------------------------------
        // ЛИНЕЙНЫЙ СНАРЯД (без/с Impact)
        // ------------------------------------------------------------
        public sealed class StraightDamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  Damage = 25f;
            public float  Speed = 20f;
            public float  MaxRange = 20f;
            public float  TickEvery = 0.02f;
            public float  HitRadius = 0.5f;
            public bool   Flat = true;
            public bool   StopOnFirstHit = true;
            public HitFilter Filter = HitFilter.Enemies;
            public bool   HitSameTargetOnce = true;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxHit;   public string? PlaySfxHit;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult StraightDamage(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            StraightDamageConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();

            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            Vector3 start = caster.Position;
            Vector3 to    = targetRef.Position;
            Vector3 dir   = to - start;
            if (cfg.Flat) dir.Z = 0f;
            var lenSq = dir.LengthSquared();
            if (lenSq < 1e-6f) dir = caster.Forward;
            if (cfg.Flat) dir.Z = 0f;
            if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
            dir = Vector3.Normalize(dir);

            float tick = MathF.Max(0.01f, cfg.TickEvery);
            float maxLife = MathF.Max(0.05f, cfg.MaxRange / MathF.Max(0.01f, cfg.Speed) + tick);
            Vector3 pos = start;
            float traveled = 0f;

            var hitSet = cfg.HitSameTargetOnce ? new HashSet<ulong>() : null;
            bool finished = false;

            rt.StartPeriodic(csid, csid, cfg.SpellId, maxLife, tick,
                onTick: () =>
                {
                    if (finished) return;

                    float step = cfg.Speed * tick;
                    var delta = dir * step;
                    pos += delta;
                    traveled += step;

                    float hitR2 = cfg.HitRadius * cfg.HitRadius;

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!PassesFilter(rt, caster, t, cfg.Filter)) continue;

                        if (hitSet != null && hitSet.Contains((ulong)rt.SidOf(t))) continue;

                        var tp = t.Position;
                        var dx = tp.X - pos.X; var dy = tp.Y - pos.Y; var dz = tp.Z - pos.Z;
                        var dist2 = dx * dx + dy * dy + dz * dz;
                        if (dist2 > hitR2) continue;

                        int tsid = rt.SidOf(t);
                        if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;

                        var resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                        var dmg = MathF.Max(0f, cfg.Damage * (1f - resist));
                        if (dmg <= 0f) continue;

                        rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                        ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, cfg.School));

                        if (!string.IsNullOrEmpty(cfg.PlayFxHit))  rt.Fx(cfg.PlayFxHit!, t);
                        if (!string.IsNullOrEmpty(cfg.PlaySfxHit)) rt.Sfx(cfg.PlaySfxHit!, t);

                        if (hitSet != null) hitSet.Add((ulong)tsid);
                        if (cfg.StopOnFirstHit) { finished = true; break; }
                    }

                    if (traveled >= cfg.MaxRange) finished = true;
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, caster);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public static SpellResult StraightDamageWithImpact(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            StraightDamageConfig cfg,
            ImpactOptions impact)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();

            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            Vector3 start = caster.Position;
            Vector3 to    = targetRef.Position;
            Vector3 dir   = to - start;
            if (cfg.Flat) dir.Z = 0f;
            var lenSq = dir.LengthSquared();
            if (lenSq < 1e-6f) dir = caster.Forward;
            if (cfg.Flat) dir.Z = 0f;
            if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
            dir = Vector3.Normalize(dir);

            float tick = MathF.Max(0.01f, cfg.TickEvery);
            float maxLife = MathF.Max(0.05f, cfg.MaxRange / MathF.Max(0.01f, cfg.Speed) + tick);
            Vector3 pos = start;
            float traveled = 0f;

            var hitSet = cfg.HitSameTargetOnce ? new HashSet<ulong>() : null;
            bool finished = false;

            rt.StartPeriodic(csid, csid, cfg.SpellId, maxLife, tick,
                onTick: () =>
                {
                    if (finished) return;

                    float step = cfg.Speed * tick;
                    var delta = dir * step;
                    pos += delta;
                    traveled += step;

                    float hitR2 = cfg.HitRadius * cfg.HitRadius;

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!PassesFilter(rt, caster, t, cfg.Filter)) continue;

                        if (hitSet != null && hitSet.Contains((ulong)rt.SidOf(t))) continue;

                        var tp = t.Position;
                        var dx = tp.X - pos.X; var dy = tp.Y - pos.Y; var dz = tp.Z - pos.Z;
                        var dist2 = dx * dx + dy * dy + dz * dz;
                        if (dist2 > hitR2) continue;

                        int tsid = rt.SidOf(t);
                        if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;

                        var resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                        var dmg = MathF.Max(0f, cfg.Damage * (1f - resist));
                        if (dmg <= 0f) continue;

                        rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                        ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, cfg.School));

                        if (!string.IsNullOrEmpty(cfg.PlayFxHit))  rt.Fx(cfg.PlayFxHit!, t);
                        if (!string.IsNullOrEmpty(cfg.PlaySfxHit)) rt.Sfx(cfg.PlaySfxHit!, t);

                        ApplyImpact(rt, caster, t, candidates, cfg.SpellId, impact);

                        if (hitSet != null) hitSet.Add((ulong)tsid);
                        if (cfg.StopOnFirstHit) { finished = true; break; }
                    }

                    if (traveled >= cfg.MaxRange) finished = true;
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, caster);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ------------------------------------------------------------
        // САМОHАВОДЯЩИЙСЯ СНАРЯД (без/с Impact)
        // ------------------------------------------------------------
        public sealed class HomingDamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  Damage = 20f;
            public float  Speed = 18f;
            public float  MaxLife = 2.5f;
            public float  TickEvery = 0.02f;
            public float  HitRadius = 0.5f;
            public bool   Flat = true;
            public HitFilter Filter = HitFilter.Enemies;
            public bool   LoseIfTargetDead = true;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxHit;   public string? PlaySfxHit;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult HomingDamage(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            HomingDamageConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            float tick = MathF.Max(0.01f, cfg.TickEvery);
            float life = MathF.Max(0.05f, cfg.MaxLife);
            Vector3 pos = caster.Position;
            bool finished = false;

            rt.StartPeriodic(csid, csid, cfg.SpellId, life, tick,
                onTick: () =>
                {
                    if (finished) return;
                    if (cfg.LoseIfTargetDead && !rt.IsAlive(targetRef)) { finished = true; return; }

                    Vector3 to = targetRef.Position;
                    Vector3 dir = to - pos;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
                    dir = Vector3.Normalize(dir);

                    float step = cfg.Speed * tick;
                    pos += dir * step;

                    var tp = targetRef.Position;
                    var dx = tp.X - pos.X; var dy = tp.Y - pos.Y; var dz = tp.Z - pos.Z;
                    if (dx * dx + dy * dy + dz * dz <= cfg.HitRadius * cfg.HitRadius)
                    {
                        int cs = rt.SidOf(caster);
                        int ts = rt.SidOf(targetRef);
                        if (PassesFilter(rt, caster, targetRef, cfg.Filter)
                            && !rt.HasImmunity(ts, "all") && !rt.HasImmunity(ts, cfg.School))
                        {
                            var resist = Clamp01(rt.GetResist01(ts, cfg.School));
                            var dmg = MathF.Max(0f, cfg.Damage * (1f - resist));
                            if (dmg > 0f)
                            {
                                rt.DealDamage(cs, ts, cfg.SpellId, dmg, cfg.School);
                                ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)cs, (ulong)ts, dmg, cfg.School));
                                if (!string.IsNullOrEmpty(cfg.PlayFxHit))  rt.Fx(cfg.PlayFxHit!, targetRef);
                                if (!string.IsNullOrEmpty(cfg.PlaySfxHit)) rt.Sfx(cfg.PlaySfxHit!, targetRef);
                            }
                        }
                        finished = true;
                    }
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, targetRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, targetRef);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public static SpellResult HomingDamageWithImpact(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot targetRef,
            IReadOnlyList<TargetSnapshot> candidates,
            HomingDamageConfig cfg,
            ImpactOptions impact)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            float tick = MathF.Max(0.01f, cfg.TickEvery);
            float life = MathF.Max(0.05f, cfg.MaxLife);
            Vector3 pos = caster.Position;
            bool finished = false;

            rt.StartPeriodic(csid, csid, cfg.SpellId, life, tick,
                onTick: () =>
                {
                    if (finished) return;
                    if (cfg.LoseIfTargetDead && !rt.IsAlive(targetRef)) { finished = true; return; }

                    Vector3 to = targetRef.Position;
                    Vector3 dir = to - pos;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
                    dir = Vector3.Normalize(dir);

                    float step = cfg.Speed * tick;
                    pos += dir * step;

                    var tp = targetRef.Position;
                    var dx = tp.X - pos.X; var dy = tp.Y - pos.Y; var dz = tp.Z - pos.Z;
                    if (dx * dx + dy * dy + dz * dz <= cfg.HitRadius * cfg.HitRadius)
                    {
                        int cs = rt.SidOf(caster);
                        int ts = rt.SidOf(targetRef);
                        if (PassesFilter(rt, caster, targetRef, cfg.Filter)
                            && !rt.HasImmunity(ts, "all") && !rt.HasImmunity(ts, cfg.School))
                        {
                            var resist = Clamp01(rt.GetResist01(ts, cfg.School));
                            var dmg = MathF.Max(0f, cfg.Damage * (1f - resist));
                            if (dmg > 0f)
                            {
                                rt.DealDamage(cs, ts, cfg.SpellId, dmg, cfg.School);
                                ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)cs, (ulong)ts, dmg, cfg.School));
                                if (!string.IsNullOrEmpty(cfg.PlayFxHit))  rt.Fx(cfg.PlayFxHit!, targetRef);
                                if (!string.IsNullOrEmpty(cfg.PlaySfxHit)) rt.Sfx(cfg.PlaySfxHit!, targetRef);

                                ApplyImpact(rt, caster, targetRef, candidates, cfg.SpellId, impact);
                            }
                        }
                        finished = true;
                    }
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, targetRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, targetRef);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ------------------------------------------------------------
        // ПРЫГАЮЩИЙ СНАРЯД (цепочка на N целей)
        // ------------------------------------------------------------
        public sealed class ChainProjectileConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  Damage = 20f;
            public float  DamageDecayPerHop = 0.25f;
            public float  Speed = 22f;
            public float  HitRadius = 0.5f;
            public float  TickEvery = 0.02f;
            public bool   Flat = true;

            public int    MaxHops = 3;
            public float  HopRadius = 8f;
            public HitFilter Filter = HitFilter.Enemies;

            public bool   UniqueTargets = true;
            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxHit;   public string? PlaySfxHit;
            public string? PlayFxHop;   public string? PlaySfxHop;
            public string? PlayFxEnd;   public string? PlaySfxEnd;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
        }

        public static SpellResult ChainDamage(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot firstTarget,
            IReadOnlyList<TargetSnapshot> candidates,
            ChainProjectileConfig cfg)
        {
            if (!rt.IsAlive(caster) || !rt.IsAlive(firstTarget)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            float tick = MathF.Max(0.01f, cfg.TickEvery);
            Vector3 pos = caster.Position;

            var visited = cfg.UniqueTargets ? new HashSet<ulong>() : null;
            TargetSnapshot currentTarget = firstTarget;
            int hopsLeft = cfg.MaxHops;
            float currentDamage = MathF.Max(0f, cfg.Damage);
            bool finished = false;

            rt.StartPeriodic(csid, csid, cfg.SpellId,
                MathF.Max(0.1f, (cfg.MaxHops + 2) * (cfg.HopRadius / MathF.Max(1f, cfg.Speed)) + 1f),
                tick,
                onTick: () =>
                {
                    if (finished) return;
                    if (!rt.IsAlive(currentTarget)) { finished = true; return; }

                    Vector3 to = currentTarget.Position;
                    Vector3 dir = to - pos;
                    if (cfg.Flat) dir.Z = 0f;
                    if (dir.LengthSquared() < 1e-6f) dir = Vector3.UnitX;
                    dir = Vector3.Normalize(dir);

                    float step = cfg.Speed * tick;
                    pos += dir * step;

                    var tp = currentTarget.Position;
                    var dx = tp.X - pos.X; var dy = tp.Y - pos.Y; var dz = tp.Z - pos.Z;
                    if (dx * dx + dy * dy + dz * dz <= cfg.HitRadius * cfg.HitRadius)
                    {
                        int ts = rt.SidOf(currentTarget);
                        if (PassesFilter(rt, caster, currentTarget, cfg.Filter)
                            && !rt.HasImmunity(ts, "all") && !rt.HasImmunity(ts, cfg.School))
                        {
                            var resist = Clamp01(rt.GetResist01(ts, cfg.School));
                            var dmg = MathF.Max(0f, currentDamage * (1f - resist));
                            if (dmg > 0f)
                            {
                                rt.DealDamage(csid, ts, cfg.SpellId, dmg, cfg.School);
                                ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)ts, dmg, cfg.School));
                                if (!string.IsNullOrEmpty(cfg.PlayFxHit))  rt.Fx(cfg.PlayFxHit!, currentTarget);
                                if (!string.IsNullOrEmpty(cfg.PlaySfxHit)) rt.Sfx(cfg.PlaySfxHit!, currentTarget);
                            }
                        }

                        if (visited != null) visited.Add((ulong)rt.SidOf(currentTarget));

                        if (hopsLeft <= 0)
                        {
                            finished = true;
                            return;
                        }

                        // ищем следующую цель в радиусе HopRadius
                        TargetSnapshot? next = null;
                        float bestDist2 = float.MaxValue;
                        var hp = currentTarget.Position;
                        float r2 = cfg.HopRadius * cfg.HopRadius;

                        for (int i = 0; i < candidates.Count; i++)
                        {
                            var c = candidates[i];
                            if (!rt.IsAlive(c)) continue;
                            if (!PassesFilter(rt, caster, c, cfg.Filter)) continue;
                            if (visited != null && visited.Contains((ulong)rt.SidOf(c))) continue;

                            var p = c.Position;
                            var ddx = p.X - hp.X; var ddy = p.Y - hp.Y; var ddz = p.Z - hp.Z;
                            var d2 = ddx * ddx + ddy * ddy + ddz * ddz;
                            if (d2 > r2) continue;

                            if (d2 < bestDist2) { bestDist2 = d2; next = c; }
                        }

                        if (next == null)
                        {
                            finished = true;
                            return;
                        }

                        currentTarget = next.Value; // <- фикс nullable
                        hopsLeft -= 1;
                        currentDamage = MathF.Max(0f, currentDamage * (1f - Clamp01(cfg.DamageDecayPerHop)));

                        pos = hp; // начинаем следующий перелёт из точки попадания
                        if (!string.IsNullOrEmpty(cfg.PlayFxHop))  rt.Fx(cfg.PlayFxHop!, currentTarget);
                        if (!string.IsNullOrEmpty(cfg.PlaySfxHop)) rt.Sfx(cfg.PlaySfxHop!, currentTarget);
                    }
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, caster);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ------------------------------------------------------------
        // Применение Impact-эффектов к цели попадания
        // ------------------------------------------------------------
        private static void ApplyImpact(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot hitTarget,
            IReadOnlyList<TargetSnapshot> candidates,
            int spellId,
            ImpactOptions impact)
        {
            int csid = rt.SidOf(caster);
            int tsid = rt.SidOf(hitTarget);

            // 1) Одиночная аура
            if (impact.Aura is not null)
            {
                float dur = MathF.Max(0.05f, impact.Aura.Duration);
                rt.ApplyAura(csid, tsid, spellId, impact.Aura.Tag, impact.Aura.Magnitude, dur);
                ProcBus.PublishAuraApply(new ProcBus.AuraArgs(spellId, (ulong)csid, (ulong)tsid, impact.Aura.Tag, impact.Aura.Magnitude, dur));
            }

            // 2) Аура по площади вокруг попадания
            if (impact.AuraAoE is not null)
            {
                float dur = MathF.Max(0.05f, impact.AuraAoE.Duration);
                float r2 = impact.AuraAoE.Radius * impact.AuraAoE.Radius;
                var center = hitTarget.Position;

                for (int i = 0; i < candidates.Count; i++)
                {
                    var t = candidates[i];
                    if (!rt.IsAlive(t)) continue;
                    if (!PassesFilter(rt, caster, t, impact.AuraAoE.Filter)) continue;

                    var p = t.Position;
                    var dx = p.X - center.X; var dy = p.Y - center.Y; var dz = p.Z - center.Z;
                    if (dx * dx + dy * dy + dz * dz > r2) continue;

                    int tid = rt.SidOf(t);
                    rt.ApplyAura(csid, tid, spellId, impact.AuraAoE.Tag, impact.AuraAoE.Magnitude, dur);
                    ProcBus.PublishAuraApply(new ProcBus.AuraArgs(spellId, (ulong)csid, (ulong)tid, impact.AuraAoE.Tag, impact.AuraAoE.Magnitude, dur));
                }

                if (!string.IsNullOrEmpty(impact.AuraAoE.PlayFx))  rt.Fx(impact.AuraAoE.PlayFx!, hitTarget);
                if (!string.IsNullOrEmpty(impact.AuraAoE.PlaySfx)) rt.Sfx(impact.AuraAoE.PlaySfx!, hitTarget);
            }

            // 3) Цепочка ауры (прыжки) — короткий periodic без именованных параметров
            if (impact.AuraChain is not null && impact.AuraChain.MaxJumps > 0)
            {
                var icfg = impact.AuraChain;
                float hopTick = MathF.Max(0.05f, icfg.JumpDelay);
                int jumpsLeft = icfg.MaxJumps;
                float magnitude = MathF.Max(icfg.MinMagnitude, icfg.MagnitudeStart);
                var visited = new HashSet<ulong> { (ulong)tsid };
                TargetSnapshot seed = hitTarget;

                rt.StartPeriodic(csid, csid, spellId,
                    MathF.Max(hopTick * (icfg.MaxJumps + 1), 0.25f),
                    hopTick,
                    onTick: () =>
                    {
                        if (jumpsLeft <= 0) return;

                        TargetSnapshot? next = null;
                        float bestDist2 = float.MaxValue;
                        var hp = seed.Position;
                        float r2 = icfg.JumpRadius * icfg.JumpRadius;

                        for (int i = 0; i < candidates.Count; i++)
                        {
                            var t = candidates[i];
                            if (!rt.IsAlive(t)) continue;
                            if (!PassesFilter(rt, caster, t, icfg.Filter)) continue;

                            ulong uid = (ulong)rt.SidOf(t);
                            if (visited.Contains(uid)) continue;

                            var p = t.Position;
                            var dx = p.X - hp.X; var dy = p.Y - hp.Y; var dz = p.Z - hp.Z;
                            var d2 = dx * dx + dy * dy + dz * dz;
                            if (d2 > r2) continue;

                            if (d2 < bestDist2) { bestDist2 = d2; next = t; }
                        }

                        if (next == null) { jumpsLeft = 0; return; }

                        magnitude = MathF.Max(icfg.MinMagnitude, magnitude * (1f - Clamp01(icfg.MagnitudeDecayPerHop)));

                        int tid = rt.SidOf(next.Value);
                        float dur = MathF.Max(0.05f, icfg.Duration);
                        rt.ApplyAura(csid, tid, spellId, icfg.Tag, magnitude, dur);
                        ProcBus.PublishAuraApply(new ProcBus.AuraArgs(spellId, (ulong)csid, (ulong)tid, icfg.Tag, magnitude, dur));

                        if (!string.IsNullOrEmpty(icfg.PlayFxHop))  rt.Fx(icfg.PlayFxHop!, next.Value);
                        if (!string.IsNullOrEmpty(icfg.PlaySfxHop)) rt.Sfx(icfg.PlaySfxHop!, next.Value);

                        visited.Add((ulong)tid);
                        seed = next.Value; // <- фикс nullable
                        jumpsLeft -= 1;
                    },
                    onEnd: () => { });
            }

            // 4) Отбрасывание от кастера
            if (impact.Knockback is not null)
            {
                var kcfg = new Movement.KnockbackFromCasterConfig
                {
                    SpellId    = spellId,
                    Distance   = impact.Knockback.Distance,
                    ApexHeight = impact.Knockback.ApexHeight,
                    Steps      = Math.Max(1, impact.Knockback.Steps),
                    TickEvery  = MathF.Max(0.01f, impact.Knockback.TickEvery),
                    Mana       = 0f,
                    Gcd        = 0f,
                    Cooldown   = 0f,
                };
                Movement.KnockbackArcFromCaster(rt, caster, hitTarget, kcfg);
            }

            // 5) Взрыв-урон
            if (impact.Explosion is not null)
            {
                float r2 = impact.Explosion.Radius * impact.Explosion.Radius;
                var center = hitTarget.Position;

                for (int i = 0; i < candidates.Count; i++)
                {
                    var t = candidates[i];
                    if (!rt.IsAlive(t)) continue;
                    if (!PassesFilter(rt, caster, t, impact.Explosion.Filter)) continue;

                    var p = t.Position;
                    var dx = p.X - center.X; var dy = p.Y - center.Y; var dz = p.Z - center.Z;
                    if (dx * dx + dy * dy + dz * dz > r2) continue;

                    int tid = rt.SidOf(t);
                    if (rt.HasImmunity(tid, "all") || rt.HasImmunity(tid, impact.Explosion.School)) continue;
                    if (impact.Explosion.RequireNoImmunityTag != null && rt.HasImmunity(tid, impact.Explosion.RequireNoImmunityTag)) continue;

                    float resist = Clamp01(rt.GetResist01(tid, impact.Explosion.School));
                    float dmg = MathF.Max(0f, impact.Explosion.Amount * (1f - resist));
                    if (dmg <= 0f) continue;

                    rt.DealDamage(csid, tid, spellId, dmg, impact.Explosion.School);
                    ProcBus.PublishDamage(new ProcBus.DamageArgs(spellId, (ulong)csid, (ulong)tid, dmg, impact.Explosion.School));
                }

                if (!string.IsNullOrEmpty(impact.Explosion.PlayFx))  rt.Fx(impact.Explosion.PlayFx!, hitTarget);
                if (!string.IsNullOrEmpty(impact.Explosion.PlaySfx)) rt.Sfx(impact.Explosion.PlaySfx!, hitTarget);
            }
        }
    }
}