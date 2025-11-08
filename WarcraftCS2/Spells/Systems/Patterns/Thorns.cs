using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Thorns/Spikes: реактивный урон атакующему, пока на цели висит баф.
    /// Не меняет входящий урон (post-hit): триггерится от ProcBus.Damage.
    public static class Thorns
    {
        public sealed class Config
        {
            public int    SpellId;
            public string AuraTag = "thorns";
            public float  Duration = 10f;

            /// Фиксированный урон за хит (0 — отключено).
            public float  FlatPerHit = 0f;

            /// Процент от полученного урона (0..1). Работает, если FlatPerHit == 0.
            public float  PercentPerHit01 = 0.0f;

            /// Кэп на одно срабатывание (0 — без кэпа).
            public float  MaxPerHit = 0f;

            /// Школа исходящего реактивного урона. Если пусто — берём школу входящего хита.
            public string? OutSchool = "nature";

            /// Если true — срабатывает только на физический урон.
            public bool   MeleeOnly = true;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            /// /// Опциональный предикат для фильтрации (targetSid, attackerSid, incomingAmount, incomingSchool) → bool.
            public Func<int,int,float,string,bool>? ExtraFilter;

            public string? PlayFx;
            public string? PlaySfx;
        }

        public static SpellResult Apply(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);
            ulong csidU = (ulong)csid, tsidU = (ulong)tsid;

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var dur = MathF.Max(0.05f, cfg.Duration);

            // Пометим ауру (для UI/диспела)
            rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.AuraTag, cfg.FlatPerHit > 0 ? cfg.FlatPerHit : cfg.PercentPerHit01, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            var sub = ProcBus.SubscribeDamage(d =>
            {
                if (d.TgtSid != tsidU) return;                 // бьют нашу цель
                if (d.SrcSid == tsidU) return;                 // самоповреждение не отражаем

                var attacker = (int)d.SrcSid;
                var amountIn = d.Amount;
                var schoolIn = d.School ?? "physical";

                if (cfg.MeleeOnly && !string.Equals(schoolIn, "physical", StringComparison.OrdinalIgnoreCase))
                    return;

                if (cfg.ExtraFilter != null)
                {
                    bool ok = false;
                    try { ok = cfg.ExtraFilter(tsid, attacker, amountIn, schoolIn); } catch { ok = false; }
                    if (!ok) return;
                }

                float outAmount = 0f;
                if (cfg.FlatPerHit > 0f)
                    outAmount = cfg.FlatPerHit;
                else if (cfg.PercentPerHit01 > 0f)
                    outAmount = amountIn * MathF.Max(0f, MathF.Min(1f, cfg.PercentPerHit01));

                if (cfg.MaxPerHit > 0f) outAmount = MathF.Min(outAmount, cfg.MaxPerHit);
                if (outAmount <= 0f) return;

                var schoolOut = string.IsNullOrEmpty(cfg.OutSchool) ? schoolIn : cfg.OutSchool!;
                rt.DealDamage(tsid, attacker, cfg.SpellId, outAmount, schoolOut);

                ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, tsidU, d.SrcSid, outAmount, schoolOut));
            });

            // Таймер завершения через Periodic как «таймер»
            rt.StartPeriodic(
                csid, tsid, cfg.SpellId,
                dur, dur,
                onTick: () => { },
                onEnd: () =>
                {
                    try { sub.Dispose(); } catch { }
                    rt.RemoveAuraByTag(tsid, cfg.AuraTag);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}