using System;
using System.Numerics;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Перемещение: блинки/даши/толкания без физики (шаговым смещением)
    public static class Movement
    {
        // ---------- BLINK ----------
        public sealed class BlinkConfig
        {
            public int SpellId;
            public Vector3 Offset;   // трактовка offset на твоей стороне рантайма
            public bool IsOffset = true;

            public float Mana = 0;
            public float Gcd = 0;
            public float Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult Blink(ISpellRuntime rt, TargetSnapshot caster, BlinkConfig cfg)
        {
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var ok = rt.TryBlink(csid, cfg.Offset, cfg.IsOffset);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);

            return ok ? SpellResult.Ok(cfg.Mana, cfg.Cooldown) : SpellResult.Fail();
        }

        // ---------- PUSH ----------
        public sealed class PushConfig
        {
            public int    SpellId;
            public float  Distance = 2.5f;
            public float  ApexHeight = 0.0f; // 0 => без дуги, просто шаг
            public int    Steps = 1;
            public float  TickEvery = 0.05f;
            public bool   Flat = true;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        /// Толчок цели от кастера (шагами, опционально «по дуге»).
        public static SpellResult Push(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, PushConfig cfg)
        {
            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var cpos = caster.Position;
            var tpos = target.Position;

            if (cfg.Flat) { cpos.Z = 0f; tpos.Z = 0f; }

            var dir = tpos - cpos;
            var len = dir.Length();
            if (len < 1e-6f) return SpellResult.Fail();
            dir /= len;

            var steps = Math.Max(1, cfg.Steps);
            var tick  = MathF.Max(0.01f, cfg.TickEvery);
            var dur   = steps * tick;

            var step = dir * (cfg.Distance / steps);
            float prevH = 0f;
            int i = 0;

            rt.StartPeriodic(csid, tsid, cfg.SpellId, dur, tick,
                onTick: () =>
                {
                    if (!rt.IsAlive(target)) return;

                    float dZ = 0f;
                    if (cfg.ApexHeight > 0f)
                    {
                        float t = (i + 1) / (float)steps; // 0..1
                        float currH = cfg.ApexHeight * MathF.Sin(MathF.PI * t);
                        dZ = (currH - prevH);
                        prevH = currH;
                    }

                    var delta = new Vector3(step.X, step.Y, dZ);
                    rt.TryBlink(tsid, delta, true);
                    i++;
                },
                onEnd: () => { });

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------- PULL ----------
        public sealed class PullConfig
        {
            public int    SpellId;
            public float  Distance = 2.5f;
            public float  ApexHeight = 0.0f;
            public int    Steps = 1;
            public float  TickEvery = 0.05f;
            public bool   Flat = true;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        /// Подтянуть цель к кастеру (шагами, опционально «по дуге»).
        public static SpellResult Pull(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, PullConfig cfg)
        {
            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var cpos = caster.Position;
            var tpos = target.Position;

            if (cfg.Flat) { cpos.Z = 0f; tpos.Z = 0f; }

            var dir = cpos - tpos;
            var len = dir.Length();
            if (len < 1e-6f) return SpellResult.Fail();
            dir /= len;

            var steps = Math.Max(1, cfg.Steps);
            var tick  = MathF.Max(0.01f, cfg.TickEvery);
            var dur   = steps * tick;

            var step = dir * (cfg.Distance / steps);
            float prevH = 0f;
            int i = 0;

            rt.StartPeriodic(csid, tsid, cfg.SpellId, dur, tick,
                onTick: () =>
                {
                    if (!rt.IsAlive(target)) return;

                    float dZ = 0f;
                    if (cfg.ApexHeight > 0f)
                    {
                        float t = (i + 1) / (float)steps;
                        float currH = cfg.ApexHeight * MathF.Sin(MathF.PI * t);
                        dZ = (currH - prevH);
                        prevH = currH;
                    }

                    var delta = new Vector3(step.X, step.Y, dZ);
                    rt.TryBlink(tsid, delta, true);
                    i++;
                },
                onEnd: () => { });

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------- KNOCKBACK по указанному горизонтальному направлению ----------
        public sealed class KnockbackArcConfig
        {
            public int    SpellId;
            public Vector3 HorizontalDir = new Vector3(1, 0, 0);
            public float  Distance = 4f;
            public float  ApexHeight = 1.0f;
            public int    Steps = 10;
            public float  TickEvery = 0.05f;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult KnockbackArc(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, KnockbackArcConfig cfg)
        {
            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var hdir = cfg.HorizontalDir;
            if (hdir.LengthSquared() < 1e-6f) return SpellResult.Fail();
            hdir = Vector3.Normalize(hdir);

            var steps = Math.Max(1, cfg.Steps);
            var tick  = MathF.Max(0.01f, cfg.TickEvery);
            var dur   = steps * tick;

            var horizStep = hdir * (cfg.Distance / steps);
            float prevH = 0f;
            int i = 0;

            rt.StartPeriodic(csid, tsid, cfg.SpellId, dur, tick,
                onTick: () =>
                {
                    if (!rt.IsAlive(target)) return;

                    float t = (i + 1) / (float)steps; // 0..1
                    float currH = cfg.ApexHeight * MathF.Sin(MathF.PI * t);
                    float dH = currH - prevH;
                    prevH = currH;

                    var delta = new Vector3(horizStep.X, horizStep.Y, dH);
                    rt.TryBlink(tsid, delta, true);
                    i++;
                },
                onEnd: () => { });

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------- KNOCKBACK от кастера по направлению «от кастера» (дугой) ----------
        public sealed class KnockbackFromCasterConfig
        {
            public int    SpellId;
            public float  Distance = 4f;
            public float  ApexHeight = 1.0f;
            public int    Steps = 10;
            public float  TickEvery = 0.05f;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;

            public string? PlayFx; public string? PlaySfx;
        }

        public static SpellResult KnockbackArcFromCaster(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, KnockbackFromCasterConfig cfg)
        {
            var hdir = DirCasterToTarget(caster, target, flat: true);
            if (hdir.LengthSquared() < 1e-6f) return SpellResult.Fail();

            return KnockbackArc(rt, caster, target, new KnockbackArcConfig
            {
                SpellId = cfg.SpellId,
                HorizontalDir = hdir,
                Distance = cfg.Distance,
                ApexHeight = cfg.ApexHeight,
                Steps = cfg.Steps,
                TickEvery = cfg.TickEvery,

                Mana = cfg.Mana, Gcd = cfg.Gcd, Cooldown = cfg.Cooldown,
                PlayFx = cfg.PlayFx, PlaySfx = cfg.PlaySfx
            });
        }

        // ---------- DASH FORWARD ----------
        public sealed class DashForwardConfig
        {
            public int    SpellId;
            public float  Distance = 5f;
            public int    Steps = 8;
            public float  TickEvery = 0.04f;

            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        /// Рывок вперёд вдоль взгляда кастера (XY-плоскость), пошагово.
        public static SpellResult DashForward(ISpellRuntime rt, TargetSnapshot caster, DashForwardConfig cfg)
        {
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var fwd = caster.Forward; fwd.Z = 0f;
            if (fwd.LengthSquared() < 1e-6f) fwd = Vector3.UnitX;
            fwd = Vector3.Normalize(fwd);

            var steps = Math.Max(1, cfg.Steps);
            var tick  = MathF.Max(0.01f, cfg.TickEvery);
            var dur   = steps * tick;
            var step  = fwd * (cfg.Distance / steps);

            int i = 0;
            rt.StartPeriodic(csid, csid, cfg.SpellId, dur, tick,
                onTick: () =>
                {
                    rt.TryBlink(csid, step, true);
                    i++;
                },
                onEnd: () => { });

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, caster);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, caster);
            return SpellResult.Ok(cfg.Mana, cfg.Cooldown);
        }

        // ---------- TELEPORT BEHIND TARGET ----------
        public sealed class TeleportBehindConfig
        {
            public int    SpellId;
            public float  Behind = 1.0f;  // на сколько позади цели
            public float  Lateral = 0.0f; // боковое смещение (влево +, вправо -)
            public bool   Flat = true;    // выравнивать по Z
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        /// Телепортирует кастера за спину цели (мгновенно).
        public static SpellResult TeleportBehindTarget(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, TeleportBehindConfig cfg)
        {
            var csid = rt.SidOf(caster);
            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();

            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var tfwd = target.Forward; if (cfg.Flat) tfwd.Z = 0f;
            if (tfwd.LengthSquared() < 1e-6f) tfwd = Vector3.UnitX;
            tfwd = Vector3.Normalize(tfwd);

            // перпендикуляр в плоскости XY: поворот на +90°
            var side = new Vector3(-tfwd.Y, tfwd.X, cfg.Flat ? 0f : tfwd.Z);
            if (side.LengthSquared() > 1e-6f) side = Vector3.Normalize(side);

            var desired = target.Position - tfwd * cfg.Behind + side * cfg.Lateral;
            if (cfg.Flat) desired.Z = caster.Position.Z; // не трогаем высоту

            var delta = desired - caster.Position;
            var ok = rt.TryBlink(csid, delta, true);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, target);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, target);
            return ok ? SpellResult.Ok(cfg.Mana, cfg.Cooldown) : SpellResult.Fail();
        }

        // ---------- SWAP POSITIONS ----------
        public sealed class SwapConfig
        {
            public int    SpellId;
            public bool   Flat = true;
            public float  Mana = 0;
            public float  Gcd = 0;
            public float  Cooldown = 0;
            public string? PlayFx; public string? PlaySfx;
        }

        /// Меняет местами позиции кастера и цели.
        public static SpellResult SwapWithTarget(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, SwapConfig cfg)
        {
            if (!rt.IsAlive(caster) || !rt.IsAlive(target)) return SpellResult.Fail();

            var csid = rt.SidOf(caster);
            var tsid = rt.SidOf(target);

            if (cfg.Mana > 0 && !rt.HasMana(csid, cfg.Mana)) return SpellResult.Fail();
            if (cfg.Mana > 0) rt.ConsumeMana(csid, cfg.Mana);
            if (cfg.Gcd  > 0) rt.StartGcd(csid, cfg.Gcd);
            if (cfg.Cooldown > 0) rt.StartCooldown(csid, cfg.SpellId, cfg.Cooldown);

            var cpos = caster.Position;
            var tpos = target.Position;
            if (cfg.Flat) { cpos.Z = 0f; tpos.Z = 0f; }

            var deltaC = tpos - cpos;   // куда переместить кастера
            var deltaT = cpos - tpos;   // куда переместить цель

            var ok1 = rt.TryBlink(csid, deltaC, true);
            var ok2 = rt.TryBlink(tsid, deltaT, true);

            if (!string.IsNullOrEmpty(cfg.PlayFx))  { rt.Fx(cfg.PlayFx!, caster); rt.Fx(cfg.PlayFx!, target); }
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) { rt.Sfx(cfg.PlaySfx!, caster); rt.Sfx(cfg.PlaySfx!, target); }

            return (ok1 && ok2) ? SpellResult.Ok(cfg.Mana, cfg.Cooldown) : SpellResult.Fail();
        }

        // ---------- HELPERS из снапшотов ----------
        private static Vector3 SnapPos(in TargetSnapshot s) => s.Position;

        public static Vector3 DirFromTo(Vector3 from, Vector3 to, bool flat = true)
        {
            var d = to - from;
            if (flat) d.Z = 0f;
            var lenSq = d.LengthSquared();
            return lenSq > 1e-6f ? d / MathF.Sqrt(lenSq) : Vector3.Zero;
        }

        public static Vector3 DirCasterToTarget(in TargetSnapshot caster, in TargetSnapshot target, bool flat = true)
            => DirFromTo(SnapPos(caster), SnapPos(target), flat);
    }

    // ------------------------------------------------------------
    // BLINK convenience API (в одном файле; делегирует в Movement.*)
    // ------------------------------------------------------------
    public static class Blink
    {
        public sealed class Config
        {
            public int   SpellId;
            public float Mana = 0f;
            public float Gcd  = 0f;
            public float Cooldown = 0f;
            public string? PlayFx;
            public string? PlaySfx;
        }

        // Блинк вперёд вдоль взгляда кастера (делегирует в DashForward)
        public static SpellResult Forward(ISpellRuntime rt, TargetSnapshot caster, float distance, Config cfg)
        {
            var dash = new Movement.DashForwardConfig {
                SpellId = cfg.SpellId, Distance = distance,
                Steps = 8, TickEvery = 0.04f,
                Mana = cfg.Mana, Gcd = cfg.Gcd, Cooldown = cfg.Cooldown,
                PlayFx = cfg.PlayFx, PlaySfx = cfg.PlaySfx
            };
            return Movement.DashForward(rt, caster, dash);
        }

        // Блинк к цели (опционально остановиться на stopShort до неё)
        public static SpellResult ToTarget(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg, float stopShort = 0f)
        {
            var cpos = caster.Position;
            var tpos = target.Position;
            if (stopShort > 0f)
            {
                var dir = tpos - cpos; dir.Z = 0f;
                var len = dir.Length();
                dir = len > 1e-6f ? dir / len : Vector3.Zero;
                tpos -= dir * stopShort;
            }
            var b = new Movement.BlinkConfig {
                SpellId = cfg.SpellId,
                Offset = tpos, IsOffset = false,
                Mana = cfg.Mana, Gcd = cfg.Gcd, Cooldown = cfg.Cooldown,
                PlayFx = cfg.PlayFx, PlaySfx = cfg.PlaySfx
            };
            return Movement.Blink(rt, caster, b);
        }

        // Блинк за спину цели (делегирует в TeleportBehindTarget)
        public static SpellResult BehindTarget(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg, float distanceBehind)
        {
            var tb = new Movement.TeleportBehindConfig {
                SpellId = cfg.SpellId, Behind = distanceBehind, Lateral = 0f, Flat = true,
                Mana = cfg.Mana, Gcd = cfg.Gcd, Cooldown = cfg.Cooldown,
                PlayFx = cfg.PlayFx, PlaySfx = cfg.PlaySfx
            };
            return Movement.TeleportBehindTarget(rt, caster, target, tb);
        }

        // Поменяться местами
        public static SpellResult SwapPositions(ISpellRuntime rt, TargetSnapshot caster, TargetSnapshot target, Config cfg)
        {
            var sc = new Movement.SwapConfig {
                SpellId = cfg.SpellId,
                Mana = cfg.Mana, Gcd = cfg.Gcd, Cooldown = cfg.Cooldown,
                PlayFx = cfg.PlayFx, PlaySfx = cfg.PlaySfx
            };
            return Movement.SwapWithTarget(rt, caster, target, sc);
        }
    }
}