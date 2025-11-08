using System;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    public static class Charge
    {
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public sealed class Config
        {
            public int    SpellId;

            // Кинематика
            public float  Speed = 8f;          // м/с
            public float  TickEvery = 0.03f;   // период тика
            public float  MaxDuration = 1.5f;  // хард-лимит времени рывка
            public float  StopAtRange = 1.0f;  // остановиться, если ближе этого расстояния

            // Импакт при завершении
            public float  ImpactDamage = 0f;   // 0 — нет урона
            public string ImpactSchool = "physical";
            public string? ImpactControlTag;   // null/"" — нет контроля
            public float  ImpactControlDuration = 0f;

            // Биллинг
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string? PlayFxStart;
            public string? PlaySfxStart;
            public string? PlayFxImpact;
            public string? PlaySfxImpact;
        }

        public static SpellResult ToTarget(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlive(caster) || !rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, caster);

            var tick   = MathF.Max(0.01f, cfg.TickEvery);
            var maxDur = MathF.Max(tick, cfg.MaxDuration);
            var stopR  = MathF.Max(0f, cfg.StopAtRange);

            // направление берём по позициям снапшотов (плоско по Z)
            Vector3 startToTarget = Movement.DirCasterToTarget(caster, target, flat: true);
            if (startToTarget == Vector3.Zero) startToTarget = Vector3.UnitX;
            Vector3 stepDir = startToTarget; // unit
            float stepLen = cfg.Speed * tick;

            bool finished = false;

            rt.StartPeriodic(
                csid, tsid, cfg.SpellId,
                maxDur, tick,
                onTick: () =>
                {
                    if (finished) return;
                    if (!rt.IsAlive(caster) || !rt.IsAlive(target))
                    {
                        finished = true;
                        return;
                    }

                    var casterPos = caster.Position;
                    var targetPos = target.Position;
                    var d = targetPos - casterPos; d.Z = 0f;
                    var dist = d.Length();

                    if (dist <= stopR + 0.001f)
                    {
                        finished = true;
                        return;
                    }

                    float need = MathF.Max(0f, dist - stopR);
                    float step = MathF.Min(stepLen, need);
                    var delta = stepDir * step;

                    rt.TryBlink(csid, delta, true);
                },
                onEnd: () =>
                {
                    if (!rt.IsAlive(target)) return;

                    // Импакт
                    if (cfg.ImpactDamage > 0f)
                    {
                        var resist = Clamp01(rt.GetResist01(tsid, cfg.ImpactSchool));
                        var dmg = MathF.Max(0f, cfg.ImpactDamage * (1f - resist));
                        if (dmg > 0f) rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.ImpactSchool);
                    }

                    if (!string.IsNullOrEmpty(cfg.ImpactControlTag) && cfg.ImpactControlDuration > 0f)
                    {
                        rt.ApplyControlWithDr(csid, tsid, cfg.SpellId, cfg.ImpactControlTag!, MathF.Max(0.05f, cfg.ImpactControlDuration));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxImpact))  rt.Fx(cfg.PlayFxImpact!, target);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxImpact)) rt.Sfx(cfg.PlaySfxImpact!, target);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}