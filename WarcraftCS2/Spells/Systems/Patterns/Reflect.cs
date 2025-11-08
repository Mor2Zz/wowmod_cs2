using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Рефлект урона. Пока висит аура на цели — часть входящего урона летит обратно в атакующего.
    public static class Reflect
    {
        public sealed class Config
        {
            public int    SpellId;
            public string AuraTag = "reflect";
            public float  Duration = 3f;

            /// Доля отражения 0..1.
            public float  Percent01 = 0.3f;

            /// Лимит отражения на один входящий хит (0 — без лимита).
            public float  MaxPerHit = 0f;

            /// Школа урона для отражения. Если null/пусто — берём школу входящего хита.
            public string? OutSchool = null;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            /// Если true и задан FriendlyPredicate — игнорировать урон от союзников (не отражать).
            public bool IgnoreFriendly = false;

            /// Опциональный предикат "союзник?". Сигнатура: (targetSid, attackerSid) -> true если союзники.
            public Func<int, int, bool>? FriendlyPredicate;

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

            // вешаем "ауру-рефлект" на цель (для UI/диспела)
            rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.AuraTag, cfg.Percent01, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            // подписка на урон: когда цель получает урон — отражаем в атакующего
            var sub = ProcBus.SubscribeDamage(d =>
            {
                if (d.TgtSid != tsidU) return;           // урон должен прилететь в нашу цель
                if (d.SrcSid == tsidU) return;           // не отражаем сам в себя (splash/self)

                var attackerSid = (int)d.SrcSid;

                // опционально игнорируем союзников, если дан предикат
                if (cfg.IgnoreFriendly && cfg.FriendlyPredicate != null)
                {
                    try
                    {
                        if (cfg.FriendlyPredicate(tsid, attackerSid)) return;
                    }
                    catch { /* не валим тред при исключении в коллбэке */ }
                }

                var reflect = d.Amount * MathF.Max(0f, MathF.Min(1f, cfg.Percent01));
                if (cfg.MaxPerHit > 0f) reflect = MathF.Min(reflect, cfg.MaxPerHit);
                if (reflect <= 0f) return;

                var school = string.IsNullOrEmpty(cfg.OutSchool) ? d.School : cfg.OutSchool!;
                rt.DealDamage(tsid, attackerSid, cfg.SpellId, reflect, school);

                // события
                ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, tsidU, d.SrcSid, reflect, school));
            });

            // таймер на окончание: используем StartPeriodic как таймер (tick == duration)
            rt.StartPeriodic(
                csid, tsid, cfg.SpellId,
                dur, dur,
                onTick: () => { /* ничего — просто дожидаемся конца */ },
                onEnd: () =>
                {
                    try { sub.Dispose(); } catch { }
                    rt.RemoveAuraByTag(tsid, cfg.AuraTag);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}