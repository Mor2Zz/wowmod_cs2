using System;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Stealth/Vanish: вешает на кастера ауру "stealth", снимает по контролю/урону.
    public static class Stealth
    {
        public sealed class Config
        {
            public int    SpellId;
            public string Tag = "stealth";
            public float  Duration = 6f;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            /// Снимать стелс при получении контроля
            public bool BreakOnControl = true;

            /// Снимать стелс при получении урона
            public bool BreakOnDamage = true;

            /// Доп. условие отмены стелса
            public Func<bool>? ExtraCancel;

            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult Apply(ISpellRuntime rt, TargetSnapshot caster, Config cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            ulong csidU = (ulong)csid;

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var dur = MathF.Max(0.05f, cfg.Duration);

            // вешаем «stealth» на кастера (value = 1)
            rt.ApplyAura(csid, csid, cfg.SpellId, cfg.Tag, 1f, dur);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            bool cancelRequested = false;
            IDisposable? subCtl = null, subDmg = null;

            if (cfg.BreakOnControl)
                subCtl = ProcBus.SubscribeControlApply(a => { if (a.TgtSid == csidU) cancelRequested = true; });

            if (cfg.BreakOnDamage)
                subDmg = ProcBus.SubscribeDamage(d => { if (d.TgtSid == csidU) cancelRequested = true; });

            // используем Periodic как таймер
            rt.StartPeriodic(
                csid, csid, cfg.SpellId,
                dur, dur,
                onTick: () => { },
                onEnd: () =>
                {
                    try { subCtl?.Dispose(); } catch { }
                    try { subDmg?.Dispose(); } catch { }

                    bool extra = false;
                    try { extra = (cfg.ExtraCancel != null && cfg.ExtraCancel()); } catch { }

                    if (cancelRequested || extra)
                        rt.RemoveAuraByTag(csid, cfg.Tag);
                    else
                        rt.RemoveAuraByTag(csid, cfg.Tag); // истекло — тоже снимаем
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}