using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.LineOfSight;
using static WarcraftCS2.Spells.Systems.Patterns.Directional; // для HitFilter

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Стационарные "облака": урон/хил/ауры по тикам в радиусе вокруг фиксированной точки.
    public static class Ground
    {
        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        // Локальный фильтр (не зависит от Directional.PassesFilter)
        private static bool Passes(ISpellRuntime rt, in TargetSnapshot caster, in TargetSnapshot t, HitFilter f)
            => f switch
            {
                HitFilter.Enemies => rt.IsEnemy(caster, t),
                HitFilter.Allies  => rt.IsAlly(caster, t),
                _                 => true
            };

        // ==============================
        // УРОН В ЗОНЕ (DoT по тикам)
        // ==============================
        public sealed class DamageConfig
        {
            public int    SpellId;
            public string School = "magic";
            public float  TickDamage = 8f;

            public float  Radius = 4f;
            public float  Duration = 6f;
            public float  TickEvery = 0.5f;
            public bool   Flat = true;         // игнорировать Z
            public HitFilter Filter = HitFilter.Enemies;

            // LoS options
            public bool   RequireLoSFromCaster = false;
            public bool   WorldOnly = false;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxTick;  public string? PlaySfxTick;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        // Ставит облако урона с центром в позиции centerRef (позиция фиксируется при старте).
        public static SpellResult Damage(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot centerRef,
            IReadOnlyList<TargetSnapshot> candidates,
            DamageConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, centerRef);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, centerRef);

            Vector3 center = centerRef.Position; // «прибили» к земле при старте
            float r2 = cfg.Radius * cfg.Radius;
            float tick = MathF.Max(0.05f, cfg.TickEvery);

            rt.StartPeriodic(
                csid, csid, cfg.SpellId,
                MathF.Max(0.05f, cfg.Duration),
                tick,
                onTick: () =>
                {
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!Passes(rt, caster, t, cfg.Filter)) continue;

                        var p = t.Position;
                        float dx = p.X - center.X, dy = p.Y - center.Y, dz = p.Z - center.Z;
                        if (cfg.Flat) dz = 0f;
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 > r2) continue;

                        if (cfg.RequireLoSFromCaster)
                        {
                            bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                            if (!ok) continue;
                        }

                        int tsid = rt.SidOf(t);
                        if (rt.HasImmunity(tsid, "all") || rt.HasImmunity(tsid, cfg.School)) continue;

                        float resist = Clamp01(rt.GetResist01(tsid, cfg.School));
                        float dmg = MathF.Max(0f, cfg.TickDamage * (1f - resist));
                        if (dmg <= 0f) continue;

                        rt.DealDamage(csid, tsid, cfg.SpellId, dmg, cfg.School);
                        ProcBus.PublishDamage(new ProcBus.DamageArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, dmg, cfg.School));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, centerRef);
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, centerRef);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ==============================
        // ХИЛ В ЗОНЕ (HoT по тикам)
        // ==============================
        public sealed class HealConfig
        {
            public int   SpellId;
            public float TickAmount = 6f;

            public float  Radius = 4f;
            public float  Duration = 6f;
            public float  TickEvery = 0.5f;
            public bool   Flat = true;
            public bool   IncludeSelf = true;

            public HitFilter Filter = HitFilter.Allies;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxTick;  public string? PlaySfxTick;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult Heal(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot centerRef,
            IReadOnlyList<TargetSnapshot> candidates,
            HealConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, centerRef);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, centerRef);

            Vector3 center = centerRef.Position;
            float r2 = cfg.Radius * cfg.Radius;
            float tick = MathF.Max(0.05f, cfg.TickEvery);

            rt.StartPeriodic(
                csid, csid, cfg.SpellId,
                MathF.Max(0.05f, cfg.Duration),
                tick,
                onTick: () =>
                {
                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!rt.IsAlly(caster, t)) continue;
                        if (!cfg.IncludeSelf && rt.SidOf(t) == csid) continue;

                        var p = t.Position;
                        float dx = p.X - center.X, dy = p.Y - center.Y, dz = p.Z - center.Z;
                        if (cfg.Flat) dz = 0f;
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 > r2) continue;

                        int tsid = rt.SidOf(t);
                        rt.Heal(csid, tsid, cfg.SpellId, MathF.Max(0f, cfg.TickAmount));
                    }

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, centerRef);
                },
                onEnd: () =>
                {
                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, centerRef);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ==============================
        // АУРА В ЗОНЕ (вход/выход)
        // ==============================
        public sealed class AuraConfig
        {
            public int    SpellId;
            public string Tag = "ground-aura";
            public float  Magnitude = 1f;

            public float  Radius = 4f;
            public float  Duration = 6f;     // жизнь самой зоны
            public float  TickEvery = 0.2f;  // частота проверки вход/выход
            public bool   Flat = true;
            public HitFilter Filter = HitFilter.All; // на кого вешаем ауру внутри

            // Если true — обновляем ауру каждый тик короткой длительностью
            public bool   RefreshEachTick = false;
            public float  RefreshDuration = 0.3f;

            // LoS options
            public bool   RequireLoSFromCaster = false;
            public bool   WorldOnly = false;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFxStart; public string? PlaySfxStart;
            public string? PlayFxEnter; public string? PlaySfxEnter;
            public string? PlayFxExit;  public string? PlaySfxExit;
            public string? PlayFxTick;  public string? PlaySfxTick;
            public string? PlayFxEnd;   public string? PlaySfxEnd;
        }

        public static SpellResult Aura(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot centerRef,
            IReadOnlyList<TargetSnapshot> candidates,
            AuraConfig cfg)
        {
            if (!rt.IsAlive(caster)) return SpellResult.Fail();
            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            if (!string.IsNullOrEmpty(cfg.PlayFxStart))  rt.Fx(cfg.PlayFxStart!, centerRef);
            if (!string.IsNullOrEmpty(cfg.PlaySfxStart)) rt.Sfx(cfg.PlaySfxStart!, centerRef);

            Vector3 center = centerRef.Position;
            float r2 = cfg.Radius * cfg.Radius;
            float tick = MathF.Max(0.05f, cfg.TickEvery);

            // Отслеживаем, кто «находится» внутри — чтобы снимать ауру по выходу
            var inside = new HashSet<ulong>();

            rt.StartPeriodic(
                csid, csid, cfg.SpellId,
                MathF.Max(0.05f, cfg.Duration),
                tick,
                onTick: () =>
                {
                    // текущее множество
                    var now = new HashSet<ulong>();

                    for (int i = 0; i < candidates.Count; i++)
                    {
                        var t = candidates[i];
                        if (!rt.IsAlive(t)) continue;
                        if (!Passes(rt, caster, t, cfg.Filter)) continue;

                        var p = t.Position;
                        float dx = p.X - center.X, dy = p.Y - center.Y, dz = p.Z - center.Z;
                        if (cfg.Flat) dz = 0f;
                        float d2 = dx * dx + dy * dy + dz * dz;
                        if (d2 > r2) continue;

                        if (cfg.RequireLoSFromCaster)
                        {
                            bool ok = cfg.WorldOnly ? LineOfSight.VisibleWorldOnly(caster, t) : LineOfSight.Visible(caster, t);
                            if (!ok) continue;
                        }

                        int tsid = rt.SidOf(t);
                        ulong uid = (ulong)tsid;
                        now.Add(uid);

                        if (!inside.Contains(uid))
                        {
                            // вход
                            if (cfg.RefreshEachTick)
                            {
                                float dur = MathF.Max(0.05f, cfg.RefreshDuration);
                                rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);

                                if (!string.IsNullOrEmpty(cfg.PlayFxEnter)) rt.Fx(cfg.PlayFxEnter!, centerRef);
                                if (!string.IsNullOrEmpty(cfg.PlaySfxEnter)) rt.Sfx(cfg.PlaySfxEnter!, centerRef);
                            }
                            else
                            {
                                // аура живёт столько же, сколько зона (минус шаг)
                                float dur = MathF.Max(0.05f, cfg.Duration - tick);
                                rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.Tag, cfg.Magnitude, dur);

                                if (!string.IsNullOrEmpty(cfg.PlayFxEnter)) rt.Fx(cfg.PlayFxEnter!, centerRef);
                                if (!string.IsNullOrEmpty(cfg.PlaySfxEnter)) rt.Sfx(cfg.PlaySfxEnter!, centerRef);
                            }
                        }
                    }

                    // обработка выхода из зоны
                    {
                        foreach (var uid in inside)
                        {
                            if (!now.Contains(uid))
                            {
                                rt.RemoveAuraByTag((int)uid, cfg.Tag);
                                if (!string.IsNullOrEmpty(cfg.PlayFxExit))  rt.Fx(cfg.PlayFxExit!, centerRef);
                                if (!string.IsNullOrEmpty(cfg.PlaySfxExit)) rt.Sfx(cfg.PlaySfxExit!, centerRef);
                            }
                        }
                    }

                    inside = now;

                    if (!string.IsNullOrEmpty(cfg.PlayFxTick))  rt.Fx(cfg.PlayFxTick!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxTick)) rt.Sfx(cfg.PlaySfxTick!, centerRef);
                },
                onEnd: () =>
                {
                    // зачистка хвостов
                    foreach (var uid in inside)
                        rt.RemoveAuraByTag((int)uid, cfg.Tag);

                    if (!string.IsNullOrEmpty(cfg.PlayFxEnd))  rt.Fx(cfg.PlayFxEnd!, centerRef);
                    if (!string.IsNullOrEmpty(cfg.PlaySfxEnd)) rt.Sfx(cfg.PlaySfxEnd!, centerRef);
                });

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }
    }
}