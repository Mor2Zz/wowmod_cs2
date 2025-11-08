using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    public static class Reactive
    {
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // ----------------------- THORNS / REFLECT -----------------------
        public sealed class ThornsConfig
        {
            public int    SpellId;
            public float  Duration = 10f;
            public float  Percent01 = 0.3f;      // 0..1
            public float  FlatBonus = 0f;
            public string School = "physical";
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string AuraTag = "thorns_active";
            public int    MaxProcs = 0;          // 0 — без лимита
            public float  PerAttackerIcd = 0f;   // сек
            public float  GlobalIcd = 0f;        // сек
            public Func<int,int,float,string,bool>? ExtraFilter;
        }

        public static SpellResult Thorns(ISpellRuntime rt, TargetSnapshot owner, ThornsConfig cfg)
        {
            if (!rt.IsAlive(owner)) return SpellResult.Fail();

            int osid = rt.SidOf(owner);
            ulong osidU = (ulong)osid;

            if (cfg.Mana > 0 && !rt.HasMana(osid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0) rt.ConsumeMana(osid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(osid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(osid, cfg.SpellId, cfg.Cooldown);

            float dur = MathF.Max(0.05f, cfg.Duration);
            rt.ApplyAura(osid, osid, cfg.SpellId, cfg.AuraTag, 1f, dur);

            int procCount = 0;
            var perAtkNext = new Dictionary<ulong, DateTimeOffset>(16);
            DateTimeOffset globalNext = DateTimeOffset.MinValue;

            var sub = ProcBus.SubscribeDamage(d =>
            {
                if (d.TgtSid != osidU) return;

                ulong atkU = d.SrcSid;
                int atkSid = (int)atkU;
                float taken = d.Amount;
                string school = d.School ?? "physical";

                if (cfg.MaxProcs > 0 && procCount >= cfg.MaxProcs) return;

                var now = DateTimeOffset.UtcNow;
                if (cfg.GlobalIcd > 0 && now < globalNext) return;
                if (cfg.PerAttackerIcd > 0 && perAtkNext.TryGetValue(atkU, out var next) && now < next) return;

                if (cfg.ExtraFilter != null)
                {
                    bool ok = false;
                    try { ok = cfg.ExtraFilter(osid, atkSid, taken, school); } catch { ok = false; }
                    if (!ok) return;
                }

                if (d.SpellId == cfg.SpellId) return;

                float reflect = MathF.Max(0f, taken * Clamp01(cfg.Percent01) + cfg.FlatBonus);
                if (reflect <= 0f) return;

                rt.DealDamage(osid, atkSid, cfg.SpellId, reflect, cfg.School);

                procCount++;
                if (cfg.GlobalIcd > 0) globalNext = now.AddSeconds(cfg.GlobalIcd);
                if (cfg.PerAttackerIcd > 0) perAtkNext[atkU] = now.AddSeconds(cfg.PerAttackerIcd);
            });

            rt.StartPeriodic(osid, osid, cfg.SpellId, dur, dur,
                onTick: () => { },
                onEnd: () =>
                {
                    try { sub.Dispose(); } catch { }
                    rt.RemoveAuraByTag(osid, cfg.AuraTag);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ----------------------- ON-DAMAGED -> CONTROL -----------------------
        public sealed class OnDamagedControlConfig
        {
            public int    SpellId;
            public string ControlTag = "stun";
            public float  Duration = 1.0f;     // до DR
            public float  BuffDuration = 10f;  // время активности реакции
            public float  Chance01 = 1f;       // 0..1
            public float  MinTakenDamage = 0f;
            public float  PerAttackerIcd = 0f;
            public float  GlobalIcd = 0f;
            public int    MaxProcs = 0;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string AuraTag = "ondamaged_control_active";
            public Func<int,int,float,string,bool>? ExtraFilter;
        }

        public static SpellResult OnDamagedControl(ISpellRuntime rt, TargetSnapshot owner, OnDamagedControlConfig cfg)
        {
            if (!rt.IsAlive(owner)) return SpellResult.Fail();

            int osid = rt.SidOf(owner);
            ulong osidU = (ulong)osid;

            if (cfg.Mana > 0 && !rt.HasMana(osid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0) rt.ConsumeMana(osid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(osid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(osid, cfg.SpellId, cfg.Cooldown);

            float activeFor = MathF.Max(0.05f, cfg.BuffDuration);
            rt.ApplyAura(osid, osid, cfg.SpellId, cfg.AuraTag, 1f, activeFor);

            int procCount = 0;
            var perAtkNext = new Dictionary<ulong, DateTimeOffset>(16);
            DateTimeOffset globalNext = DateTimeOffset.MinValue;
            var rng = new Random();

            var sub = ProcBus.SubscribeDamage(d =>
            {
                if (d.TgtSid != osidU) return;

                ulong atkU = d.SrcSid;
                int atkSid = (int)atkU;
                float taken = d.Amount;
                string school = d.School ?? "physical";

                if (taken < cfg.MinTakenDamage) return;
                if (cfg.MaxProcs > 0 && procCount >= cfg.MaxProcs) return;

                var now = DateTimeOffset.UtcNow;
                if (cfg.GlobalIcd > 0 && now < globalNext) return;
                if (cfg.PerAttackerIcd > 0 && perAtkNext.TryGetValue(atkU, out var next) && now < next) return;

                if (cfg.ExtraFilter != null)
                {
                    bool ok = false;
                    try { ok = cfg.ExtraFilter(osid, atkSid, taken, school); } catch { ok = false; }
                    if (!ok) return;
                }

                var chance = Clamp01(cfg.Chance01);
                lock (rng) { if (rng.NextDouble() > chance) return; }

                var applied = rt.ApplyControlWithDr(osid, atkSid, cfg.SpellId, cfg.ControlTag, MathF.Max(0.05f, cfg.Duration));
                if (applied <= 0) return;

                procCount++;
                if (cfg.GlobalIcd > 0) globalNext = now.AddSeconds(cfg.GlobalIcd);
                if (cfg.PerAttackerIcd > 0) perAtkNext[atkU] = now.AddSeconds(cfg.PerAttackerIcd);
                ProcBus.PublishControlApply(new ProcBus.ControlArgs(cfg.SpellId, (ulong)osid, atkU, cfg.ControlTag, applied));
            });

            rt.StartPeriodic(osid, osid, cfg.SpellId, activeFor, activeFor,
                onTick: () => { },
                onEnd: () =>
                {
                    try { sub.Dispose(); } catch { }
                    rt.RemoveAuraByTag(osid, cfg.AuraTag);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ----------------------- LIFESTEAL BUFF (вампиризм по исходящему урону) -----------------------
        public sealed class LifestealBuffConfig
        {
            public int    SpellId;
            public float  Duration   = 10f;    // срок действия баффа
            public float  Percent01  = 0.25f;  // доля исходящего урона -> хил (0..1)
            public float  FlatBonus  = 0f;     // фикс. добавка к хилу
            public string? SchoolFilter;       // null/"" — все школы
            public float  GlobalIcd  = 0f;     // анти-спам тиков, сек
            public int    MaxProcs   = 0;      // 0 — без лимита

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string AuraTag = "lifesteal_active";
            public Func<int,int,float,string,bool>? ExtraFilter; // (owner, victim, dealt, school) -> ok?
        }

        // Бафф на владельца: когда он наносит урон, он исцеляется.
        public static SpellResult LifestealBuff(ISpellRuntime rt, TargetSnapshot owner, LifestealBuffConfig cfg)
        {
            if (!rt.IsAlive(owner)) return SpellResult.Fail();

            int osid = rt.SidOf(owner);
            ulong osidU = (ulong)osid;

            if (cfg.Mana > 0 && !rt.HasMana(osid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0) rt.ConsumeMana(osid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(osid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(osid, cfg.SpellId, cfg.Cooldown);

            float dur = MathF.Max(0.05f, cfg.Duration);
            rt.ApplyAura(osid, osid, cfg.SpellId, cfg.AuraTag, 1f, dur);

            int procCount = 0;
            DateTimeOffset nextAllowed = DateTimeOffset.MinValue;

            var sub = ProcBus.SubscribeDamage(d =>
            {
                // интересует исходящий урон от владельца
                if (d.SrcSid != osidU) return;

                int victimSid = (int)d.TgtSid;
                float dealt   = d.Amount;
                string school = d.School ?? "physical";

                if (!string.IsNullOrEmpty(cfg.SchoolFilter) &&
                    !string.Equals(cfg.SchoolFilter, school, StringComparison.OrdinalIgnoreCase))
                    return;

                if (cfg.MaxProcs > 0 && procCount >= cfg.MaxProcs) return;

                var now = DateTimeOffset.UtcNow;
                if (cfg.GlobalIcd > 0 && now < nextAllowed) return;

                if (cfg.ExtraFilter != null)
                {
                    bool ok = false;
                    try { ok = cfg.ExtraFilter(osid, victimSid, dealt, school); } catch { ok = false; }
                    if (!ok) return;
                }

                float heal = MathF.Max(0f, dealt * Clamp01(cfg.Percent01) + cfg.FlatBonus);
                if (heal <= 0f) return;

                rt.Heal(osid, osid, cfg.SpellId, heal);

                procCount++;
                if (cfg.GlobalIcd > 0) nextAllowed = now.AddSeconds(cfg.GlobalIcd);
            });

            // авто-очистка по завершению
            rt.StartPeriodic(osid, osid, cfg.SpellId, dur, dur,
                onTick: () => { },
                onEnd: () =>
                {
                    try { sub.Dispose(); } catch { }
                    rt.RemoveAuraByTag(osid, cfg.AuraTag);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}