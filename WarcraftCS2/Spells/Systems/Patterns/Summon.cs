using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using static WarcraftCS2.Spells.Systems.Patterns.Directional; // HitFilter

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // "Мягкий" тотем/маяк: точка-эмиттер, живёт N секунд, прерывается если кастер умер/ушёл далеко.
    public static class Summon
    {
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static bool Passes(ISpellRuntime rt, in TargetSnapshot caster, in TargetSnapshot t, HitFilter f)
            => f switch
            {
                HitFilter.Enemies => rt.IsEnemy(caster, t),
                HitFilter.Allies  => rt.IsAlly(caster, t),
                _                 => true
            };

        // ==============================
        // ТОТЕМ УРОНА
        // ==============================
        public sealed class DamageTotemConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  TickDamage = 6f;

            public float  Radius = 5f;
            public float  Duration = 8f;
            public float  TickEvery = 0.5f;
            public bool   Flat = true;
            public HitFilter Filter = HitFilter.Enemies;

            public bool   BreakIfCasterDead = true;
            public float  LeashRange = 50f;     // если кастер ушёл дальше — тотем пропадает (<=0: без поводка)

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxTick;  public string? PlaySfxTick;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult DamageTotem(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot placeAt,
            IReadOnlyList<TargetSnapshot> candidates,
            DamageTotemConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();

            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, placeAt);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, placeAt);

            Vector3 center = placeAt.Position;
            float r2 = cfg.Radius * cfg.Radius;
            float leash2 = cfg.LeashRange > 0f ? cfg.LeashRange * cfg.LeashRange : -1f;
            float tick = MathF.Max(0.05f, cfg.TickEvery);

            rt.StartPeriodic(
                csid, csid, cfg.SpellId,
                MathF.Max(0.05f, cfg.Duration),
                tick,
                onTick: () =>
                {
                    if (cfg.BreakIfCasterDead && !rt.IsAlive(caster))
                        return;

                    if (leash2 > 0f)
                    {
                        Vector3 cp = caster.Position;
                        Vector3 d  = cp - center;
                        if (cfg.Flat) d.Z = 0f;
                        if (d.LengthSquared() > leash2) return;
                    }

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!Passes(rt, caster, t, cfg.Filter)) continue;

                        var p = t.Position;
                        float dx = p.X - center.X, dy = p.Y - center.Y, dz = p.Z - center.Z;
                        if (cfg.Flat) dz = 0f;
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 > r2) continue;

                        int tsid = rt.SidOf(t);
                        if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;

                        float resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                        float dmg = MathF.Max(0f, cfg.TickDamage * (1f - resist));
                        if (dmg <= 0f) continue;

                        rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                        ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, cfg.School));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, placeAt);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, placeAt);
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, placeAt);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, placeAt);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ==============================
        // ТОТЕМ АУРЫ
        // ==============================
        public sealed class AuraTotemConfig
        {
            public int    SpellId;
            public string Tag = "totem-aura";
            public float  Magnitude = 1f;

            public float  Radius = 5f;
            public float  Duration = 8f;
            public float  TickEvery = 0.25f;
            public bool   Flat = true;
            public HitFilter Filter = HitFilter.All;

            public bool   RefreshEachTick = false;
            public float  RefreshDuration = 0.35f;

            public bool   BreakIfCasterDead = true;
            public float  LeashRange = 50f;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxEnter; public string? PlaySfxEnter;
            public string? PlayFxExit;  public string? PlaySfxExit;
            public string? PlayFxTick;  public string? PlaySfxTick;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult AuraTotem(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot placeAt,
            IReadOnlyList<TargetSnapshot> candidates,
            AuraTotemConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();

            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, placeAt);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, placeAt);

            Vector3 center = placeAt.Position;
            float r2 = cfg.Radius * cfg.Radius;
            float leash2 = cfg.LeashRange > 0f ? cfg.LeashRange * cfg.LeashRange : -1f;
            float tick = MathF.Max(0.05f, cfg.TickEvery);

            var inside = new HashSet<ulong>();

            rt.StartPeriodic(
                csid, csid, cfg.SpellId,
                MathF.Max(0.05f, cfg.Duration),
                tick,
                onTick: () =>
                {
                    if (cfg.BreakIfCasterDead && !rt.IsAlive(caster))
                        return;

                    if (leash2 > 0f)
                    {
                        Vector3 cp = caster.Position;
                        Vector3 d  = cp - center;
                        if (cfg.Flat) d.Z = 0f;
                        if (d.LengthSquared() > leash2) return;
                    }

                    var now = new HashSet<ulong>();

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!Passes(rt, caster, t, cfg.Filter)) continue;

                        var p = t.Position;
                        float dx = p.X - center.X, dy = p.Y - center.Y, dz = p.Z - center.Z;
                        if (cfg.Flat) dz = 0f;
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 > r2) continue;

                        int tsid = rt.SidOf(t);
                        ulong uid = (ulong)tsid;
                        now.Add(uid);

                        if (!inside.Contains(uid))
                        {
                            float dur = cfg.RefreshEachTick ? MathF.Max(0.05f, cfg.RefreshDuration)
                                                            : MathF.Max(0.05f, cfg.Duration);
                            rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);
                            ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, cfg.Tag, cfg.Magnitude, dur));

                            if (!string.IsNullOrEmpty(cfg.PlayFxEnter))  rt.Fx(cfg.PlayFxEnter!, t);
                            if (!string.IsNullOrEmpty(cfg.PlaySfxEnter)) rt.Sfx(cfg.PlaySfxEnter!, t);
                        }
                        else if (cfg.RefreshEachTick)
                        {
                            float dur = MathF.Max(0.05f, cfg.RefreshDuration);
                            rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);
                            ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, cfg.Tag, cfg.Magnitude, dur));
                        }
                    }

                    if (!cfg.RefreshEachTick)
                    {
                        foreach (var uid in inside)
                        {
                            if (!now.Contains(uid))
                            {
                                rt.RemoveAuraByTag((int)uid, cfg.Tag);
                                if (!string.IsNullOrEmpty(cfg.PlayFxExit))  rt.Fx(cfg.PlayFxExit!, placeAt);
                                if (!string.IsNullOrEmpty(cfg.PlaySfxExit)) rt.Sfx(cfg.PlaySfxExit!, placeAt);
                            }
                        }
                    }

                    inside = now;

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, placeAt);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, placeAt);
                },
                onEnd: () =>
                {
                    foreach (var uid in inside)
                        rt.RemoveAuraByTag((int)uid, cfg.Tag);

                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, placeAt);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, placeAt);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}