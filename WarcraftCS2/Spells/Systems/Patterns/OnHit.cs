using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Универсальные on-hit проки: пока эффект активен на владельце, каждое его попадание по цели
    /// может применить ауру или нанести дополнительный урон. Есть шансы, ICd (per target / global) и т.п.
    public static class OnHit
    {
        private static readonly Random _rng = new Random();
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // =====================================================================
        //  AURA ON-HIT: при попадании вешает ауру на цель (с лимитами/ICD)
        // =====================================================================
        public sealed class AuraConfig
        {
            public int   SpellId;
            public string Tag = "onhit_aura";
            public float  ApplyValue = 1f;
            public float  ApplyDuration = 3f;

            /// Сколько секунд активна САМА ПРОКА (подписка), а не аура на цели.
            public float  Duration = 10f;

            /// Вероятность срабатывания 0..1 на каждый хит.
            public float  Chance01 = 1f;

            /// Только физический входящий урон (определяем по d.School == "physical").
            public bool   OnlyPhysical = false;

            /// Не срабатывать, если исходный урон меньше порога.
            public float  MinIncomingDamage = 0f;

            /// Лимит срабатываний за всю жизнь эффекта (0/отриц — без лимита).
            public int    MaxProcs = 0;

            /// Internal Cooldown по цели, сек (0 — нет).
            public float  PerTargetIcd = 0f;

            /// Глобальный ICD между срабатываниями, сек (0 — нет).
            public float  GlobalIcd = 0f;

            /// Фильтр (srcSid=владелец, tgtSid=цель удара, amount, school) → true если срабатываем.
            public Func<int,int,float,string,bool>? ExtraFilter;

            // Биллинг «активации прока» (стоимость применения эффекта на владельца)
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
        }

        /// Активирует на кастере «прок по попаданию»: при его ударах вешаем ауру на цель.
        public static SpellResult ApplyAuraOnHit(ISpellRuntime rt, TargetSnapshot owner, AuraConfig cfg)
        {
            if (!rt.IsAlive(owner)) return SpellResult.Fail();

            var osid = rt.SidOf(owner);
            ulong osidU = (ulong)osid;

            if (cfg.Mana > 0 && !rt.HasMana(osid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0) rt.ConsumeMana(osid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(osid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(osid, cfg.SpellId, cfg.Cooldown);

            var dur = MathF.Max(0.05f, cfg.Duration);
            var applyDur = MathF.Max(0.05f, cfg.ApplyDuration);

            int procCount = 0;
            var perTargetNext = new Dictionary<ulong, DateTimeOffset>(16);
            DateTimeOffset globalNext = DateTimeOffset.MinValue;
            bool inProc = false; // защита от самоповтора, если событие синхронно

            var sub = ProcBus.SubscribeDamage(d =>
            {
                // Нужен исходящий урон ОТ владельца
                if (d.SrcSid != osidU) return;

                var tgtU = d.TgtSid;
                var tgtSid = (int)tgtU;
                var amount = d.Amount;
                var school = d.School ?? "physical";

                if (cfg.OnlyPhysical && !string.Equals(school, "physical", StringComparison.OrdinalIgnoreCase)) return;
                if (amount < cfg.MinIncomingDamage) return;
                if (cfg.MaxProcs > 0 && procCount >= cfg.MaxProcs) return;

                var now = DateTimeOffset.UtcNow;
                if (cfg.GlobalIcd > 0 && now < globalNext) return;

                if (cfg.PerTargetIcd > 0 && perTargetNext.TryGetValue(tgtU, out var next) && now < next) return;

                if (cfg.ExtraFilter != null)
                {
                    bool ok = false;
                    try { ok = cfg.ExtraFilter(osid, tgtSid, amount, school); } catch { ok = false; }
                    if (!ok) return;
                }

                // Chance roll
                if (Clamp01(cfg.Chance01) < 1f)
                {
                    lock (_rng)
                    {
                        if (_rng.NextDouble() > Clamp01(cfg.Chance01))
                            return;
                    }
                }

                if (inProc) return; // защита от самоповтора
                inProc = true;
                try
                {
                    rt.ApplyAura(osid, tgtSid, cfg.SpellId, cfg.Tag, cfg.ApplyValue, applyDur);
                    ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, osidU, tgtU, cfg.Tag, cfg.ApplyValue, applyDur));
                    procCount++;

                    if (cfg.GlobalIcd > 0) globalNext = now.AddSeconds(cfg.GlobalIcd);
                    if (cfg.PerTargetIcd > 0) perTargetNext[tgtU] = now.AddSeconds(cfg.PerTargetIcd);
                }
                finally { inProc = false; }
            });

            // Вешаем «маркер-саму проку» на владельца для UI/диспела (опционально тем же спелл-айди)
            rt.ApplyAura(osid, osid, cfg.SpellId, "onhit_active", 1f, dur);

            // Таймер окончания жизни эффекта
            rt.StartPeriodic(
                osid, osid, cfg.SpellId,
                dur, dur,
                onTick: () => { },
                onEnd: () =>
                {
                    try { sub.Dispose(); } catch { }
                    rt.RemoveAuraByTag(osid, "onhit_active");
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // =====================================================================
        //  EXTRA DAMAGE ON-HIT: при попадании наносит доп. урон по цели
        // =====================================================================
        public sealed class ExtraDamageConfig
        {
            public int    SpellId;
            public string OutSchool = "magic";

            /// Приоритет: FlatPerHit, иначе PercentOfOutgoing01 * исходный урон.
            public float  FlatPerHit = 0f;
            public float  PercentOfOutgoing01 = 0.25f;

            public float  Duration = 10f;
            public float  Chance01 = 1f;
            public bool   OnlyPhysical = false;
            public float  MinIncomingDamage = 0f;

            public float  MaxPerHit = 0f; // 0 — без кэпа

            public float  PerTargetIcd = 0f;
            public float  GlobalIcd = 0f;
            public int    MaxProcs = 0;

            public Func<int,int,float,string,bool>? ExtraFilter;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
        }

        /// Активирует на кастере «прок по попаданию»: наносит дополнительный урон цели.
        public static SpellResult ApplyExtraDamageOnHit(ISpellRuntime rt, TargetSnapshot owner, ExtraDamageConfig cfg)
        {
            if (!rt.IsAlive(owner)) return SpellResult.Fail();

            var osid = rt.SidOf(owner);
            ulong osidU = (ulong)osid;

            if (cfg.Mana > 0 && !rt.HasMana(osid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0) rt.ConsumeMana(osid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(osid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(osid, cfg.SpellId, cfg.Cooldown);

            var dur = MathF.Max(0.05f, cfg.Duration);

            int procCount = 0;
            var perTargetNext = new Dictionary<ulong, DateTimeOffset>(16);
            DateTimeOffset globalNext = DateTimeOffset.MinValue;
            bool inProc = false; // чтобы наш дополнительный урон не закачивал сам себя

            var sub = ProcBus.SubscribeDamage(d =>
            {
                if (d.SrcSid != osidU) return;

                var tgtU = d.TgtSid;
                var tgtSid = (int)tgtU;
                var amount = d.Amount;
                var schoolIn = d.School ?? "physical";

                if (cfg.OnlyPhysical && !string.Equals(schoolIn, "physical", StringComparison.OrdinalIgnoreCase)) return;
                if (amount < cfg.MinIncomingDamage) return;
                if (cfg.MaxProcs > 0 && procCount >= cfg.MaxProcs) return;

                var now = DateTimeOffset.UtcNow;
                if (cfg.GlobalIcd > 0 && now < globalNext) return;
                if (cfg.PerTargetIcd > 0 && perTargetNext.TryGetValue(tgtU, out var next) && now < next) return;

                if (cfg.ExtraFilter != null)
                {
                    bool ok = false;
                    try { ok = cfg.ExtraFilter(osid, tgtSid, amount, schoolIn); } catch { ok = false; }
                    if (!ok) return;
                }

                // Chance roll
                if (Clamp01(cfg.Chance01) < 1f)
                {
                    lock (_rng)
                    {
                        if (_rng.NextDouble() > Clamp01(cfg.Chance01))
                            return;
                    }
                }

                float add = cfg.FlatPerHit > 0f ? cfg.FlatPerHit : amount * Clamp01(cfg.PercentOfOutgoing01);
                if (cfg.MaxPerHit > 0f) add = MathF.Min(add, cfg.MaxPerHit);
                if (add <= 0f) return;

                if (inProc) return; // защитимся от реэнтранси при синхронной публикации события
                inProc = true;
                try
                {
                    rt.DealDamage(osid, tgtSid, cfg.SpellId, add, cfg.OutSchool);
                    ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, osidU, tgtU, add, cfg.OutSchool));
                    procCount++;

                    if (cfg.GlobalIcd > 0) globalNext = now.AddSeconds(cfg.GlobalIcd);
                    if (cfg.PerTargetIcd > 0) perTargetNext[tgtU] = now.AddSeconds(cfg.PerTargetIcd);
                }
                finally { inProc = false; }
            });

            // Маркер активности на владельце (для UI/диспела)
            rt.ApplyAura(osid, osid, cfg.SpellId, "onhit_active", 1f, dur);

            rt.StartPeriodic(
                osid, osid, cfg.SpellId,
                dur, dur,
                onTick: () => { },
                onEnd: () =>
                {
                    try { sub.Dispose(); } catch { }
                    rt.RemoveAuraByTag(osid, "onhit_active");
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}