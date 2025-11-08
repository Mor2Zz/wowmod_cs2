using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Зоны на земле с периодическими тиками: DoT/HoT/ауры (slow и т.п.).
    public static class GroundZone
    {
        public enum AnchorKind { Static, FollowCaster, FollowTarget }
        public enum TargetFilter { Enemies, Allies, All }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        private static bool PassesFilter(ISpellRuntime rt, in TargetSnapshot caster, in TargetSnapshot t, TargetFilter f)
            => f switch
            {
                TargetFilter.Enemies => rt.IsEnemy(caster, t),
                TargetFilter.Allies  => rt.IsAlly(caster, t),
                _                    => true
            };

        private static List<TargetSnapshot> CollectSphere(Vector3 center, float radius, IReadOnlyList<TargetSnapshot> candidates, Func<TargetSnapshot, bool> predicate)
        {
            var res = new List<TargetSnapshot>(16);
            float r2 = radius * radius;
            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                var p = t.Position;
                var dx = p.X - center.X; var dy = p.Y - center.Y; var dz = p.Z - center.Z;
                if ((dx * dx + dy * dy + dz * dz) <= r2 && predicate(t))
                    res.Add(t);
            }
            return res;
        }

        // ------------------- базовая конфигурация зоны -------------------
        public abstract class ZoneConfigBase
        {
            public int   SpellId;
            public float Radius     = 4f;
            public float Duration   = 6f;
            public float TickEvery  = 0.5f;

            public AnchorKind Anchor = AnchorKind.Static; // поведение центра
            public TargetFilter Filter = TargetFilter.Enemies;

            public float  Mana     = 0;
            public float  Gcd      = 0;
            public float  Cooldown = 0;

            public string? PlayFxStart;  public string? PlaySfxStart;
            public string? PlayFxTick;   public string? PlaySfxTick;
            public string? PlayFxEnd;    public string? PlaySfxEnd;
        }

        // ------------------- DAMAGE ZONE -------------------
        public sealed class DamageZoneConfig : ZoneConfigBase
        {
            public string School = "magic";        // "physical","holy","fire","frost","nature","shadow","arcane"
            public float  AmountPerTick = 10f;     // сырой урон на тик (до резистов)
            public string? RequireNoImmunityTag;   // доп. тэг иммунитета (кроме школ/“all”)
        }

        /// Зона урона. centerRef — от кого берём начальную точку (обычно target).
        public static SpellResult DamageZone(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot centerRef,
            IReadOnlyList<TargetSnapshot> candidates,
            DamageZoneConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            // начальный центр
            var staticCenter = centerRef.Position;

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, centerRef);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, centerRef);

            float dur  = MathF.Max(0.05f, cfg.Duration);
            float tick = MathF.Max(0.05f, cfg.TickEvery);

            rt.StartPeriodic(csid, csid, cfg.SpellId, dur, tick,
                onTick: () =>
                {
                    // вычисляем актуальный центр по якорю
                    Vector3 center = cfg.Anchor switch
                    {
                        AnchorKind.FollowCaster => caster.Position,
                        AnchorKind.FollowTarget => centerRef.Position,
                        _ => staticCenter
                    };

                    var victims = CollectSphere(center, cfg.Radius, candidates,
                        t => rt.IsAlive(t) && PassesFilter(rt, caster, t, cfg.Filter));

                    if (victims.Count == 0) return;

                    for (int i = 0; i < victims.Count; i++)
                    {
                        var v = victims[i];
                        var tsid = rt.SidOf(v);

                        if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;
                        if (cfg.RequireNoImmunityTag != null && rt.HasImmunity(tsid, cfg.RequireNoImmunityTag)) continue;

                        var resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                        var dmg = MathF.Max(0f, cfg.AmountPerTick * (1f - resist));
                        if (dmg <= 0f) continue;

                        rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);

                        // события (если нужны твоим талантам/аурум)
                        ProcBus.PublishPeriodicTick(new ProcBus.PeriodicTickArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, "dot"));
                        ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, cfg.School));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, centerRef);
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, centerRef);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ------------------- HEAL ZONE -------------------
        public sealed class HealZoneConfig : ZoneConfigBase
        {
            public float AmountPerTick = 10f;
        }

        public static SpellResult HealZone(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot centerRef,
            IReadOnlyList<TargetSnapshot> candidates,
            HealZoneConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var staticCenter = centerRef.Position;

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, centerRef);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, centerRef);

            float dur  = MathF.Max(0.05f, cfg.Duration);
            float tick = MathF.Max(0.05f, cfg.TickEvery);

            rt.StartPeriodic(csid, csid, cfg.SpellId, dur, tick,
                onTick: () =>
                {
                    Vector3 center = cfg.Anchor switch
                    {
                        AnchorKind.FollowCaster => caster.Position,
                        AnchorKind.FollowTarget => centerRef.Position,
                        _ => staticCenter
                    };

                    var mates = CollectSphere(center, cfg.Radius, candidates,
                        t => rt.IsAlive(t) && PassesFilter(rt, caster, t, cfg.Filter));

                    if (mates.Count == 0) return;

                    var amount = MathF.Max(0f, cfg.AmountPerTick);
                    if (amount <= 0f) return;

                    for (int i = 0; i < mates.Count; i++)
                    {
                        var m = mates[i];
                        var tsid = rt.SidOf(m);
                        rt.Heal(csid, tsid, cfg.SpellId, amount);
                        ProcBus.PublishPeriodicTick(new ProcBus.PeriodicTickArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, amount, "hot"));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, centerRef);
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, centerRef);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ------------------- AURA ZONE (напр. slow) -------------------
        public sealed class AuraZoneConfig : ZoneConfigBase
        {
            public string Tag = "slow";
            public float  Magnitude = 1f;     // степень эффекта
            public bool   ReapplyEachTick = true;  // просто продлеваем на TickEvery
        }

        /// На каждом тике продлевает ауру Tag у целей в зоне (или перевыдаёт).
        public static SpellResult AuraZone(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot centerRef,
            IReadOnlyList<TargetSnapshot> candidates,
            AuraZoneConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var staticCenter = centerRef.Position;

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, centerRef);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, centerRef);

            float dur  = MathF.Max(0.05f, cfg.Duration);
            float tick = MathF.Max(0.05f, cfg.TickEvery);

            rt.StartPeriodic(csid, csid, cfg.SpellId, dur, tick,
                onTick: () =>
                {
                    Vector3 center = cfg.Anchor switch
                    {
                        AnchorKind.FollowCaster => caster.Position,
                        AnchorKind.FollowTarget => centerRef.Position,
                        _ => staticCenter
                    };

                    var targets = CollectSphere(center, cfg.Radius, candidates,
                        t => rt.IsAlive(t) && PassesFilter(rt, caster, t, cfg.Filter));
                    if (targets.Count == 0) return;

                    // продлеваем на TickEvery (или минимум 0.05)
                    float reap = MathF.Max(0.05f, cfg.ReapplyEachTick ? cfg.TickEvery : cfg.Duration);

                    for (int i = 0; i < targets.Count; i++)
                    {
                        var tt = targets[i];
                        var tsid = rt.SidOf(tt);
                        rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.Tag, cfg.Magnitude, reap);
                        ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, cfg.Tag, cfg.Magnitude, reap));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, centerRef);
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, centerRef);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}