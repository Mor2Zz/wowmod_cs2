using System;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Прыжок дугой к цели: шаговое перемещение кастера в сторону цели с вертикальной синусоидой.
    /// Движение «без физики» через TryBlink; по приземлении — опциональный импакт (урон/контроль).
    public static class Leap
    {
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public sealed class ToTargetConfig
        {
            public int    SpellId;

            // Геометрия прыжка
            public float  Distance   = 6f;        // горизонтальная дальность (м)
            public float  ApexHeight = 1.5f;      // подъём по Z в середине (м)
            public int    Steps      = 10;        // количество шагов
            public float  TickEvery  = 0.05f;     // период тика (сек)

            // Импакт (по цели) после приземления
            public float  ImpactDamage     = 0f;  // 0 — без урона
            public string ImpactSchool     = "physical";
            public string? ImpactControlTag;      // null/"" — без контроля
            public float  ImpactControlDur = 0f;  // сек

            // Биллинг
            public float  Mana     = 0;
            public float  Gcd      = 0;
            public float  Cooldown = 0;

            // Эффекты (опционально)
            public string? PlayFxStart;   public string? PlaySfxStart;
            public string? PlayFxImpact;  public string? PlaySfxImpact;
        }

        /// Прыжок к текущей позиции цели (позиции берём из снапшотов).
        public static SpellResult ToTarget(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, ToTargetConfig cfg)
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

            // Горизонтальное направление — от кастера к цели (плоско по Z).
            var hdir = Movement.DirCasterToTarget(caster, target, flat: true);
            if (hdir == Vector3.Zero) hdir = Vector3.UnitX; // запасной вариант
            var steps = Math.Max(1, cfg.Steps);
            var tick  = MathF.Max(0.01f, cfg.TickEvery);
            var dur   = steps * tick;

            var horizStep = hdir * (cfg.Distance / steps);
            float prevH = 0f;
            int i = 0;

            rt.StartPeriodic(csid, tsid, cfg.SpellId, dur, tick,
                onTick: () =>
                {
                    if (!rt.IsAlive(caster)) return;

                    // Параметр дуги 0..1
                    float t = (i + 1) / (float)steps;
                    float currH = cfg.ApexHeight * MathF.Sin(MathF.PI * t);
                    float dH = currH - prevH;
                    prevH = currH;

                    var step = new Vector3(horizStep.X, horizStep.Y, dH);
                    rt.TryBlink(csid, step, true);
                    i++;
                },
                onEnd: () =>
                {
                    if (!rt.IsAlive(target)) return;

                    // Импакт по цели (простая проверка сопротивлений)
                    if (cfg.ImpactDamage > 0f)
                    {
                        var resist = Clamp01(rt.GetResist01(tsid, cfg.ImpactSchool));
                        var dmg = MathF.Max(0f, cfg.ImpactDamage * (1f - resist));
                        if (dmg > 0f) rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.ImpactSchool);
                    }

                    if (!string.IsNullOrEmpty(cfg.ImpactControlTag) && cfg.ImpactControlDur > 0f)
                    {
                        rt.ApplyControlWithDr(csid, tsid, cfg.SpellId, cfg.ImpactControlTag!, MathF.Max(0.05f, cfg.ImpactControlDur));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxImpact))  rt.Fx(cfg.PlayFxImpact!, target);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxImpact)) rt.Sfx(cfg.PlaySfxImpact!, target);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}