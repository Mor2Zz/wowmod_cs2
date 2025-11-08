using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime; // ProcBus

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Базовые баффы/дебаффы по тегу: Apply/Remove (+ AoE-выдача).
    public static class Buff
    {
        public enum Filter { All, Allies, Enemies }

        // ---- одиночная выдача по тегу ----
        public sealed class Config
        {
            public int    SpellId;
            public string Tag = "buff";
            public float  Magnitude = 1f;
            public float  Duration  = 5f;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFx; public string? PlaySfx;
        }

        static float ClampPos(float v) => v < 0f ? 0f : v;

        static SpellResult ApplyCommon(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlive(target)) return SpellResult.Fail();
            int csid = rt.SidOf(caster), tsid = rt.SidOf(target);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            float dur = MathF.Max(0.05f, cfg.Duration);
            float mag = ClampPos(cfg.Magnitude);

            rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.Tag, mag, dur);
            ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, cfg.Tag, mag, dur));

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);

            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        public static SpellResult ApplySelf(ISpellRuntime rt, TargetSnapshot caster, Config cfg)
            => ApplyCommon(rt, caster, caster, cfg);

        public static SpellResult ApplyToAlly(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsAlly(caster, target)) return SpellResult.Fail();
            return ApplyCommon(rt, caster, target, cfg);
        }

        public static SpellResult ApplyToEnemy(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            if (!rt.IsEnemy(caster, target)) return SpellResult.Fail();
            return ApplyCommon(rt, caster, target, cfg);
        }

        // ---- массовая выдача по области ----
        public sealed class AreaConfig
        {
            public int    SpellId;
            public string Tag = "buff";
            public float  Magnitude = 1f;
            public float  Duration  = 5f;

            public float  Radius = 6f;
            public bool   Flat = true;
            public int    MaxTargets = 64;
            public Filter TargetFilter = Filter.All;

            public float  Mana = 0f;
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFx; public string? PlaySfx;
        }

        /// Выдать бафф по области вокруг center среди candidates.
        public static SpellResult ApplyArea(
            ISpellRuntime rt,
            TargetSnapshot caster,
            TargetSnapshot center,
            IReadOnlyList<TargetSnapshot> candidates,
            AreaConfig cfg)
        {
            int csid = rt.SidOf(caster);
            if (cfg.Mana > 0f && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana     > 0f) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd      > 0f) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0f) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var cpos = center.Position;
            float r2 = cfg.Radius * cfg.Radius;
            int applied = 0;

            float dur = MathF.Max(0.05f, cfg.Duration);
            float mag = ClampPos(cfg.Magnitude);

            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                if (!rt.IsAlive(t)) continue;

                if (cfg.TargetFilter == Filter.Allies  && !rt.IsAlly(caster, t))  continue;
                if (cfg.TargetFilter == Filter.Enemies && !rt.IsEnemy(caster, t)) continue;

                var p = t.Position;
                float dx = p.X - cpos.X, dy = p.Y - cpos.Y, dz = p.Z - cpos.Z;
                if (cfg.Flat) dz = 0f;
                float d2 = dx*dx + dy*dy + dz*dz;
                if (d2 > r2) continue;

                int tsid = rt.SidOf(t);
                rt.ApplyAura(csid, tsid, cfg.SpellId, cfg.Tag, mag, dur);
                ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, cfg.Tag, mag, dur));

                if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, t);
                if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, t);

                applied++;
                if (applied >= cfg.MaxTargets) break;
            }

            return applied > 0 ? SpellResult.Ok(cfg.Mana, cfg.Cooldown) : SpellResult.Fail();
        }

        /// Снять по тегу.
        public static void Remove(ISpellRuntime rt, TargetSnapshot target, string tag)
        {
            int tsid = rt.SidOf(target);
            rt.RemoveAuraByTag(tsid, tag);
        }
    }
}