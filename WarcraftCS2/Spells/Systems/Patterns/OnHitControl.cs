using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Control.Break;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Контроль по попаданию владельца (шансы, ICD, DR, опционально break-on-damage)
    public static class OnHitControl
    {
        private static readonly Random Rng = new Random();
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        public sealed class Config
        {
            public int    SpellId;
            public string Tag = "stun";
            public float  Duration = 1.5f;

            public float  DurationActive = 10f;

            public float  Chance01 = 1f;
            public bool   OnlyPhysical = false;
            public float  MinOutgoingDamage = 0f;

            public float  PerTargetIcd = 0f;
            public float  GlobalIcd = 0f;
            public int    MaxProcs = 0;

            public bool   BreakOnDamage = false;
            public float  BreakFlat = 99999f;
            public float  BreakPercent01 = 1f;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public Func<int,int,float,string,bool>? ExtraFilter;
        }

        public static SpellResult Apply(ISpellRuntime rt, TargetSnapshot owner, Config cfg)
        {
            if (!rt.IsAlive(owner)) return SpellResult.Fail();

            var osid  = rt.SidOf(owner);     // int для runtime-методов
            ulong osidU = (ulong)osid;       // ulong для ProcBus/ActiveCc

            if (cfg.Mana > 0 && !rt.HasMana(osid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana     > 0) rt.ConsumeMana(osid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(osid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(osid, cfg.SpellId, cfg.Cooldown);

            var activeFor = MathF.Max(0.05f, cfg.DurationActive);
            rt.ApplyAura(osid, osid, cfg.SpellId, "onhit_control_active", 1f, activeFor);

            int procCount = 0;
            var perTargetNext = new Dictionary<ulong, DateTimeOffset>(16);
            DateTimeOffset globalNext = DateTimeOffset.MinValue;
            bool inProc = false;

            var sub = ProcBus.SubscribeDamage(d =>
            {
                if (d.SrcSid != osidU) return;

                var tgtU   = d.TgtSid;     // ulong из события
                var tgtSid = (int)tgtU;    // int для runtime-методов
                var amount = d.Amount;
                var school = d.School ?? "physical";

                if (cfg.OnlyPhysical && !string.Equals(school, "physical", StringComparison.OrdinalIgnoreCase)) return;
                if (amount < cfg.MinOutgoingDamage) return;
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

                if (Clamp01(cfg.Chance01) < 1f)
                {
                    lock (Rng)
                    {
                        if (Rng.NextDouble() > Clamp01(cfg.Chance01)) return;
                    }
                }

                if (inProc) return;
                inProc = true;
                try
                {
                    // runtime: int sid'ы
                    var applied = rt.ApplyControlWithDr(osid, tgtSid, cfg.SpellId, cfg.Tag, MathF.Max(0.05f, cfg.Duration));

                    if (applied > 0 && cfg.BreakOnDamage)
                    {
                        // BreakOnDamage: ulong sid'ы
                        var cc = new ActiveCc(osidU, tgtU, cfg.SpellId, cfg.Tag, applied, cfg.BreakFlat, cfg.BreakPercent01);
                        var targetSidCopy = tgtSid; var tagCopy = cfg.Tag; 
                        BreakOnDamageService.Instance.Register(in cc, breaker: () => rt.RemoveAuraByTag(targetSidCopy, tagCopy));
                    }

                    if (applied > 0)
                    {
                        procCount++;
                        if (cfg.GlobalIcd > 0)   globalNext = now.AddSeconds(cfg.GlobalIcd);
                        if (cfg.PerTargetIcd > 0) perTargetNext[tgtU] = now.AddSeconds(cfg.PerTargetIcd);

                        ProcBus.PublishControlApply(new ProcBus.ControlArgs(cfg.SpellId, osidU, tgtU, cfg.Tag, applied));
                    }
                }
                finally { inProc = false; }
            });

            rt.StartPeriodic(
                osid, osid, cfg.SpellId,
                activeFor, activeFor,
                onTick: () => { },
                onEnd: () =>
                {
                    try { sub.Dispose(); } catch { }
                    rt.RemoveAuraByTag(osid, "onhit_control_active");
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}