using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Цепные эффекты: хил и щит, прыгают по союзникам.
    public static class Chain
    {
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // -----------------------------------------
        // ЦЕПОЧНЫЙ ХИЛ
        // -----------------------------------------
        public sealed class ChainHealConfig
        {
            public bool RequireLoSBetweenHops = false;
            public bool WorldOnly = false;

            public int   SpellId;
            public float AmountStart = 40f;            // хил первого попадания
            public float AmountDecayPerHop = 0.2f;     // доля снижения на каждом прыжке (0..1)
            public int   MaxHops = 3;                  // доп. цели после первой
            public float HopRadius = 8f;               // поиск следующей цели
            public float HopDelay  = 0.1f;             // задержка между прыжками (сек)
            public bool  UniqueTargets = true;         // не лечить одну и ту же цель дважды

            public float Mana = 0f;
            public float Gcd  = 0f;
            public float Cooldown = 0f;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxHit;   public string? PlaySfxHit;
            public string? PlayFxHop;   public string? PlaySfxHop;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult Heal(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot firstTarget,
            IReadOnlyList<TargetSnapshot> candidates,
            ChainHealConfig cfg)
        {
            if (!rt.IsAlive(caster) || !rt.IsAlive(firstTarget)) return SpellResult.Fail();

            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            float hopTick = MathF.Max(0.05f, cfg.HopDelay);
            int   hopsLeft = cfg.MaxHops;
            float amount = MathF.Max(0f, cfg.AmountStart);
            HashSet<ulong>? visited = cfg.UniqueTargets ? new() : null;

            TargetSnapshot current = firstTarget;
            if (visited != null) visited.Add((ulong)rt.SidOf(current));

            // На первом тике сразу лечим firstTarget, затем прыгаем
            bool firstDone = false;

            rt.StartPeriodic(
                csid, csid, cfg.SpellId,
                MathF.Max(hopTick * (cfg.MaxHops + 1), 0.25f),
                hopTick,
                onTick: () =>
                {
                    if (!firstDone)
                    {
                        if (rt.IsAlive(current) && amount > 0f)
                        {
                            int tsid0 = rt.SidOf(current);
                            rt.Heal(csid, tsid0, cfg.SpellId, amount);
                            if (!string.IsNullOrEmpty(cfg.PlayFxHit))  rt.Fx(cfg.PlayFxHit!, current);
                            if (!string.IsNullOrEmpty(cfg.PlaySfxHit)) rt.Sfx(cfg.PlaySfxHit!, current);
                        }
                        firstDone = true;
                        return;
                    }

                    // поиск следующей союзной цели
                    TargetSnapshot? next = null;
                    float bestDist2 = float.MaxValue;

                    Vector3 hp = current.Position;
                    float r2 = cfg.HopRadius * cfg.HopRadius;

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];

                        if (cfg.RequireLoSBetweenHops)
                        {
                            bool visible = cfg.WorldOnly
                                ? LineOfSight.VisibleWorldOnly(current, t)
                                : LineOfSight.Visible(current, t);
                            if (!visible) continue;
                        }
                        if (!rt.IsAlive(t)) continue;
                        if (!rt.IsAlly(caster, t)) continue;

                        ulong uid = (ulong)rt.SidOf(t);
                        if (visited != null && visited.Contains(uid)) continue;

                        Vector3 p = t.Position;
                        float dx = p.X - hp.X, dy = p.Y - hp.Y, dz = p.Z - hp.Z;
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 > r2) continue;

                        if (d2 < bestDist2) { bestDist2 = d2; next = t; }
                    }

                    if (next == null) { hopsLeft = 0; return; }

                    current = next.Value;
                    if (visited != null) visited.Add((ulong)rt.SidOf(current));

                    amount = MathF.Max(0f, amount * (1f - Clamp01(cfg.AmountDecayPerHop)));

                    if (rt.IsAlive(current) && amount > 0f)
                    {
                        int tsid = rt.SidOf(current);
                        rt.Heal(csid, tsid, cfg.SpellId, amount);
                        if (!string.IsNullOrEmpty(cfg.PlayFxHop))  rt.Fx(cfg.PlayFxHop!, current);
                        if (!string.IsNullOrEmpty(cfg.PlaySfxHop)) rt.Sfx(cfg.PlaySfxHop!, current);
                    }

                    hopsLeft -= 1;
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, caster);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, caster);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // -----------------------------------------
        // ЦЕПОЧНЫЙ ЩИТ
        // -----------------------------------------
        public sealed class ChainShieldConfig
        {
            public bool RequireLoSBetweenHops = false;
            public bool WorldOnly = false;

            public int   SpellId;
            public string Tag = "shield";
            public float CapacityStart = 40f;          // ёмкость первого щита
            public float CapacityDecayPerHop = 0.2f;   // доля снижения на каждом прыжке (0..1)
            public int   MaxHops = 3;
            public float HopRadius = 8f;
            public float HopDelay  = 0.1f;
            public bool  UniqueTargets = true;

            public float Mana = 0f;
            public float Gcd  = 0f;
            public float Cooldown = 0f;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxHit;   public string? PlaySfxHit;
            public string? PlayFxHop;   public string? PlaySfxHop;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult Shield(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot firstTarget,
            IReadOnlyList<TargetSnapshot> candidates,
            ChainShieldConfig cfg)
        {
            if (!rt.IsAlive(caster) || !rt.IsAlive(firstTarget)) return SpellResult.Fail();

            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            float hopTick = MathF.Max(0.05f, cfg.HopDelay);
            int   hopsLeft = cfg.MaxHops;
            float capacity = MathF.Max(0f, cfg.CapacityStart);
            HashSet<ulong>? visited = cfg.UniqueTargets ? new() : null;

            TargetSnapshot current = firstTarget;
            if (visited != null) visited.Add((ulong)rt.SidOf(current));

            bool firstDone = false;

            rt.StartPeriodic(
                csid, csid, cfg.SpellId,
                MathF.Max(hopTick * (cfg.MaxHops + 1), 0.25f),
                hopTick,
                onTick: () =>
                {
                    if (!firstDone)
                    {
                        if (rt.IsAlive(current) && capacity > 0f)
                        {
                            int tsid0 = rt.SidOf(current);
                            float duration = 6f;
                            rt.ApplyShield(csid, tsid0, cfg.SpellId, cfg.Tag, capacity, duration);
                            ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid0, cfg.Tag, capacity, duration));
                            if (!string.IsNullOrEmpty(cfg.PlayFxHit))  rt.Fx(cfg.PlayFxHit!, current);
                            if (!string.IsNullOrEmpty(cfg.PlaySfxHit)) rt.Sfx(cfg.PlaySfxHit!, current);
                        }
                        firstDone = true;
                        return;
                    }

                    TargetSnapshot? next = null;
                    float bestDist2 = float.MaxValue;

                    Vector3 hp = current.Position;
                    float r2 = cfg.HopRadius * cfg.HopRadius;

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];

                        if (cfg.RequireLoSBetweenHops)
                        {
                            bool visible = cfg.WorldOnly
                                ? LineOfSight.VisibleWorldOnly(current, t)
                                : LineOfSight.Visible(current, t);
                            if (!visible) continue;
                        }
                        if (!rt.IsAlive(t)) continue;
                        if (!rt.IsAlly(caster, t)) continue;

                        ulong uid = (ulong)rt.SidOf(t);
                        if (visited != null && visited.Contains(uid)) continue;

                        Vector3 p = t.Position;
                        float dx = p.X - hp.X, dy = p.Y - hp.Y, dz = p.Z - hp.Z;
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 > r2) continue;

                        if (d2 < bestDist2) { bestDist2 = d2; next = t; }
                    }

                    if (next == null) { hopsLeft = 0; return; }

                    current = next.Value;
                    if (visited != null) visited.Add((ulong)rt.SidOf(current));

                    capacity = MathF.Max(0f, capacity * (1f - Clamp01(cfg.CapacityDecayPerHop)));

                    if (rt.IsAlive(current) && capacity > 0f)
                    {
                        int tsid = rt.SidOf(current);
                        float duration = 6f;
                        rt.ApplyShield(csid, tsid, cfg.SpellId, cfg.Tag, capacity, duration);
                        ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, cfg.Tag, capacity, duration));
                        if (!string.IsNullOrEmpty(cfg.PlayFxHop))  rt.Fx(cfg.PlayFxHop!, current);
                        if (!string.IsNullOrEmpty(cfg.PlaySfxHop)) rt.Sfx(cfg.PlaySfxHop!, current);
                    }

                    hopsLeft -= 1;
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