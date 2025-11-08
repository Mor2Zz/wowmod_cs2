using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// ManaShield: поддерживает на цели щит, пока есть мана у кастера.
    /// Щит вешается через ApplyShield и «продлевается» на короткое время каждый тик.
    /// Мана списывается раз в тик; если маны не хватает — баф снимается.
    public static class ManaShield
    {
        public sealed class Config
        {
            public int    SpellId;
            public string Tag = "mana_shield";

            /// Ёмкость щита, накатываемая (или поддерживаемая) каждый тик.
            public float  CapacityPerTick = 20f;

            /// Длительность поддержания эффекта (жизнь подписки/таймера), сек.
            public float  Duration = 8f;

            /// Период тика (и ребинд щита), сек.
            public float  TickEvery = 0.25f;

            /// Сколько маны списывать с кастера в секунду (фактический расход = * TickEvery).
            public float  ManaPerSecond = 10f;

            /// На сколько секунд вешаем/продлеваем щит при каждом тике.
            public float  ReapplyDuration = 0.5f;

            /// Общий кэп на суммарно добавленную ёмкость за всё время (0 — без кэпа).
            public float  MaxTotalCapacity = 0f;

            /// Кэп на разовую подкачку в моменте (0 — не ограничивать).
            public float  MaxShieldAtOnce = 0f;

            // Биллинг «каста»
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string? PlayFx;
            public string? PlaySfx;

            /// Доп. условие отмены (проверяется на каждом тике).
            public Func<bool>? ExtraCancel;
        }

        /// Поддержка щита за счёт маны кастера.
        public static SpellResult Apply(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlive(caster) || !rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);
            ulong csidU = (ulong)csid, tsidU = (ulong)tsid;

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana))
                return SpellResult.Fail();

            // стартовые биллинги
            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var dur     = MathF.Max(0.05f, cfg.Duration);
            var tick    = MathF.Max(0.05f, cfg.TickEvery);
            var reapDur = MathF.Max(0.05f, cfg.ReapplyDuration);
            var manaPerTick = MathF.Max(0f, cfg.ManaPerSecond) * tick;

            float totalAdded = 0f;
            bool cancelRequested = false;

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            // первичная подкачка щита (безопасно – ApplyShield сам агрегирует по tag/spellId)
            if (cfg.CapacityPerTick > 0f)
            {
                // ✔ исправлено: позиционные аргументы + ref totalAdded
                var cap = CapToLimits(cfg.CapacityPerTick, 0f, cfg.MaxTotalCapacity, ref totalAdded);
                if (cap > 0f)
                {
                    rt.ApplyShield(csid, tsid, cfg.SpellId, cfg.Tag, cap, reapDur);
                    ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, csidU, tsidU, cfg.Tag, cap, reapDur));
                }
            }

            // Таймером управляем через периодик
            rt.StartPeriodic(
                csid, tsid, cfg.SpellId,
                dur, tick,
                onTick: () =>
                {
                    if (cancelRequested) return;
                    if (!rt.IsAlive(caster) || !rt.IsAlive(target)) { cancelRequested = true; return; }
                    if (cfg.ExtraCancel != null)
                    {
                        bool br = false; try { br = cfg.ExtraCancel(); } catch { br = true; }
                        if (br) { cancelRequested = true; return; }
                    }

                    if (manaPerTick > 0f && !rt.HasMana(csid, manaPerTick))
                    {
                        cancelRequested = true;
                        return;
                    }

                    if (manaPerTick > 0f)
                        rt.ConsumeMana(csid, manaPerTick);

                    // добавить щит
                    var add = cfg.CapacityPerTick;
                    if (add <= 0f) return;

                    // лимиты
                    add = CapToLimits(add, cfg.MaxShieldAtOnce, cfg.MaxTotalCapacity, ref totalAdded);
                    if (add <= 0f) return;

                    rt.ApplyShield(csid, tsid, cfg.SpellId, cfg.Tag, add, reapDur);
                    ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, csidU, tsidU, cfg.Tag, add, reapDur));
                },
                onEnd: () =>
                {
                    // Снимаем щитовый тег по окончании/отмене
                    rt.RemoveAuraByTag(tsid, cfg.Tag);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        /// Применяет лимиты на разовую/общую подкачку.
        private static float CapToLimits(float add, float MaxShieldAtOnce, float MaxTotalCapacity, ref float totalAdded)
        {
            if (add <= 0f) return 0f;
            // кэп «в моменте»
            if (MaxShieldAtOnce > 0f) add = MathF.Min(add, MaxShieldAtOnce);
            if (add <= 0f) return 0f;

            // кэп «за жизнь эффекта»
            if (MaxTotalCapacity > 0f)
            {
                var remain = MaxTotalCapacity - totalAdded;
                if (remain <= 0f) return 0f;
                if (add > remain) add = remain;
            }

            totalAdded += add;
            return add;
        }
    }
}