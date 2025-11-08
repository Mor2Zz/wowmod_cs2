using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Control.Break;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Контроль с учетом DR и (опционально) брейка от урона.
    public static class Control
    {
        public sealed class Config
        {
            public int    SpellId;
            public string Tag = "stun";   // "stun","root","silence","fear","poly","disarm" и т.п.
            public float  Duration;

            public float  Mana = 0f;
            public float  Gcd = 0f;
            public float  Cooldown = 0f;

            // Break-on-damage (опционально)
            public bool   BreakOnDamage = false;
            public float  BreakFlat = 0f;        // порог урона (если > 0 — ломаем при e.Amount >= BreakFlat)
            public float  BreakPercent01 = 0f;   // под проценты (если пользуешься собственными правилами)
        }

        public static SpellResult Apply(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            // Sid'ы приходят как int => приводим к ulong для событий/сервисов
            var csidInt = rt.SidOf(caster);
            var tsidInt = rt.SidOf(target);
            ulong csid = (ulong)csidInt;
            ulong tsid = (ulong)tsidInt;

            if (cfg.Mana > 0 && !rt.HasMana(csidInt, cfg.Mana))
                return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csidInt, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csidInt, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csidInt, cfg.SpellId, cfg.Cooldown);

            // Применение контрола с DR. Возвращает фактическую длительность (>0, если применилось).
            var applied = rt.ApplyControlWithDr(csidInt, tsidInt, cfg.SpellId, cfg.Tag, MathF.Max(0.05f, cfg.Duration));

            if (applied > 0)
            {
                // Proc-событие для талантов/аур
                ProcBus.PublishControlApply(new ProcBus.ControlArgs(cfg.SpellId, csid, tsid, cfg.Tag, applied));

                // Регистрация брейка при уроне (через ProcBus.OnDamage), если включено.
                if (cfg.BreakOnDamage)
                {
                    var cc = new ActiveCc(csid, tsid, cfg.SpellId, cfg.Tag, applied, cfg.BreakFlat, cfg.BreakPercent01);

                    // локальные копии для замыкания строго того типа, который ждёт runtime-метод
                    int targetSidForRuntime = tsidInt;
                    string tag = cfg.Tag;

                    BreakOnDamageService.Instance.Register(
                        in cc,
                        breaker: () => rt.RemoveAuraByTag(targetSidForRuntime, tag) 
                    );
                }
            }

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}