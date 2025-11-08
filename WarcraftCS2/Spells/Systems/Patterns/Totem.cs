using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Patterns;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Тотем/вард: живёт N секунд в точке, каждый тик раздаёт ауру/урон/хил в радиусе.
    public static class Totem
    {
        public enum Filter { All, Allies, Enemies }

        public sealed class Config
        {
            public int    SpellId;
            public string TagAura = "totem-aura";
            public float  AuraMagnitude = 1f;
            public bool   ApplyAura = true;     // раздавать/обновлять ауру на входящих

            public float  DamagePerTick = 0f;   // наносить урон (обычно врагам)
            public string DamageSchool = "magic";
            public float  HealPerTick   = 0f;   // лечить (обычно союзников)

            public float  Duration = 8f;
            public float  TickEvery = 0.25f;
            public float  Radius = 6f;
            public bool   Flat = true;
            public int    MaxTargets = 64;
            public Filter TargetFilterForAura  = Filter.All;
            public Filter TargetFilterForDmg   = Filter.Enemies;
            public Filter TargetFilterForHeal  = Filter.Allies;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFxOnSpawn;
            public string? PlaySfxOnSpawn;
            public string? PlayFxOnTick;
            public string? PlaySfxOnTick;
            public string? PlayFxOnEnd;
            public string? PlaySfxOnEnd;
        }

        static bool Pass(ISpellRuntime rt, TargetSnapshot c, TargetSnapshot t, Filter f)
        {
            return f switch {
                Filter.Allies  => rt.IsAlly(c, t),
                Filter.Enemies => rt.IsEnemy(c, t),
                _ => true
            };
        }

        public static SpellResult Spawn(
            ISpellRuntime rt,
            TargetSnapshot owner,
            TargetSnapshot centerRef,
            IReadOnlyList<TargetSnapshot> candidates,
            Config cfg)
        {
            if (!rt.IsAlive(owner)) return SpellResult.Fail();

            int osid = rt.SidOf(owner);
            if (cfg.Mana > 0f && !rt.HasMana(osid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0f) rt.ConsumeMana(osid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(osid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(osid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxOnSpawn)) rt.Fx(cfg.PlayFxOnSpawn!, centerRef);
            if (!string.IsNullOrEmpty(cfg.PlaySfxOnSpawn)) rt.Sfx(cfg.PlaySfxOnSpawn!, centerRef);

            Vector3 center = centerRef.Position;
            float r2 = cfg.Radius * cfg.Radius;
            float tick = MathF.Max(0.05f, cfg.TickEvery);
            float dur  = MathF.Max(0.05f, cfg.Duration);

            var inside = new HashSet<ulong>(); // кто сейчас в зоне (для снятия ауры на конец)

            rt.StartPeriodic(
                osid, osid, cfg.SpellId,
                dur,
                tick,
                onTick: () =>
                {
                    if (!rt.IsAlive(owner)) return; // владелец умер — просто не делаем ничего дальше

                    int applied = 0;
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;

                        var p = t.Position;
                        float dx = p.X - center.X, dy = p.Y - center.Y, dz = p.Z - center.Z;
                        if (cfg.Flat) dz = 0f;
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 > r2) continue;

                        if (cfg.ApplyAura && Pass(rt, owner, t, cfg.TargetFilterForAura))
                        {
                            int tsid = rt.SidOf(t);
                            inside.Add((ulong)tsid);
                            rt.ApplyAura(osid, tsid, cfg.SpellId, cfg.TagAura, MathF.Max(0f, cfg.AuraMagnitude), tick * 2f);
                        }

                        if (cfg.DamagePerTick > 0f && Pass(rt, owner, t, cfg.TargetFilterForDmg))
                        {
                            int tsid = rt.SidOf(t);
                            rt.DealDamage(osid, tsid, cfg.SpellId, cfg.DamagePerTick, cfg.DamageSchool);
                        }

                        if (cfg.HealPerTick > 0f && Pass(rt, owner, t, cfg.TargetFilterForHeal))
                        {
                            int tsid = rt.SidOf(t);
                            rt.Heal(osid, tsid, cfg.SpellId, cfg.HealPerTick);
                        }

                        applied++;
                        if (applied >= cfg.MaxTargets) break;
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxOnTick))  rt.Fx(cfg.PlayFxOnTick!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxOnTick)) rt.Sfx(cfg.PlaySfxOnTick!, centerRef);
                },
                onEnd: () =>
                {
                    // чистим ауру у тех, кто остался с тегом внутри
                    foreach (var uid in inside)
                        rt.RemoveAuraByTag((int)uid, cfg.TagAura);

                    if (!string.IsNullOrEmpty(cfg.PlayFxOnEnd))  rt.Fx(cfg.PlayFxOnEnd!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxOnEnd)) rt.Sfx(cfg.PlaySfxOnEnd!, centerRef);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}
