using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Адаптивный щит: пока баф активен, при каждом входящем уроне на цель «накатывается» щит.
    public static class AdaptiveShield
    {
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public sealed class Config
        {
            public int    SpellId;

            /// Тег щита.
            public string Tag = "adaptive_shield";

            /// Длительность действия эффекта (окно, пока работают подписки).
            public float  Duration = 8f;

            /// На сколько секунд продлеваем щит при каждом срабатывании.
            public float  ReapplyDuration = 0.75f;

            /// Стартовая ёмкость щита сразу при касте (0 — не вешать).
            public float  BaseCapacity = 0f;

            /// Сколько щита добавлять за хит (если &gt; 0 — приоритет над PercentOfDamage01).
            public float  GainFlat = 0f;

            /// Доля от входящего урона, конвертируемая в щит (0..1), если GainFlat == 0.
            public float  PercentOfDamage01 = 0.35f;

            /// Кэп добавления за один хит (0 — без кэпа).
            public float  MaxPerHit = 0f;

            /// Общий кэп на суммарно добавленную ёмкость за всё время (0 — без кэпа).
            public float  MaxTotalAdded = 0f;

            /// Фильтр: реагировать только на физический урон.
            public bool   OnlyPhysical = false;

            /// Доп. фильтр: (targetSid, attackerSid, incomingAmount, incomingSchool) → true если обрабатывать хит.
            public Func<int,int,float,string,bool>? ExtraFilter;

            // Биллинг
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string? PlayFx;
            public string? PlaySfx;
        }

        /// Применить адаптивный щит на цель.
        public static SpellResult Apply(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);
            ulong csidU = (ulong)csid, tsidU = (ulong)tsid;

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana))
                return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var dur   = MathF.Max(0.05f, cfg.Duration);
            var reap  = MathF.Max(0.05f, cfg.ReapplyDuration);

            // Стартовый щит (опционально)
            if (cfg.BaseCapacity > 0f)
            {
                rt.ApplyShield(csid, tsid, cfg.SpellId, cfg.Tag, cfg.BaseCapacity, reap);
                ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, csidU, tsidU, cfg.Tag, cfg.BaseCapacity, reap));
            }

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            float totalAdded = 0f;

            // Подписка на входящий урон по цели
            var sub = ProcBus.SubscribeDamage(d =>
            {
                if (d.TgtSid != tsidU) return;                       // нужен урон по нашей цели
                if (d.Amount <= 0f) return;

                var attacker = (int)d.SrcSid;
                var school   = d.School ?? "physical";

                if (cfg.OnlyPhysical && !string.Equals(school, "physical", StringComparison.OrdinalIgnoreCase))
                    return;

                if (cfg.ExtraFilter != null)
                {
                    bool ok = false;
                    try { ok = cfg.ExtraFilter(tsid, attacker, d.Amount, school); } catch { ok = false; }
                    if (!ok) return;
                }

                float add = cfg.GainFlat > 0f
                    ? cfg.GainFlat
                    : d.Amount * Clamp01(cfg.PercentOfDamage01);

                if (cfg.MaxPerHit > 0f) add = MathF.Min(add, cfg.MaxPerHit);
                if (add <= 0f) return;

                if (cfg.MaxTotalAdded > 0f)
                {
                    var remain = cfg.MaxTotalAdded - totalAdded;
                    if (remain <= 0f) return;
                    if (add > remain) add = remain;
                }

                rt.ApplyShield(csid, tsid, cfg.SpellId, cfg.Tag, add, reap);
                ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, csidU, tsidU, cfg.Tag, add, reap));
                totalAdded += add;
            });

            // Таймер жизни эффекта — по окончании снимаем подписку и ауру (если есть)
            rt.StartPeriodic(
                csid, tsid, cfg.SpellId,
                dur, dur,
                onTick: () => { /* ничего */ },
                onEnd: () =>
                {
                    try { sub.Dispose(); } catch { /* игнор */ }
                    rt.RemoveAuraByTag(tsid, cfg.Tag);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}