using System;
using System.Collections.Generic;
using System.Numerics;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Systems.Casting
{
    public static class CastScheduler
    {
        private sealed class CastTask
        {
            public WowmodCs2 Plugin = null!;
            public CCSPlayerController Caster = null!;
            public ulong CasterSid;
            public string SpellId = "";
            public double DurationSec;
            public DateTime StartUtc;
            public bool AllowMove;
            public float MoveTolerance;
            public bool CancelOnStun;
            public bool CancelOnSilence;
            public Vector3? StartPos;  // null, если AllowMove
            public Action OnComplete = null!;
            public Action<string>? OnCancel;
            public bool Cancelled;
        }

        // casterSid -> active cast
        private static readonly Dictionary<ulong, CastTask> _tasks = new();

        public static bool IsCasting(ulong casterSid) => _tasks.ContainsKey(casterSid);

        /// Старт каст-тайма.
        public static bool BeginCast(
            WowmodCs2 plugin,
            CCSPlayerController caster,
            string spellId,
            double durationSec,
            Action onComplete,
            Action<string>? onCancel = null,
            bool allowMove = false,
            float moveTolerance = 12f,
            bool cancelOnStun = true,
            bool cancelOnSilence = true)
        {
            if (plugin is null || caster is null || !caster.IsValid || durationSec <= 0 || onComplete is null)
                return false;

            var sid = (ulong)caster.SteamID;

            // Если уже кастует — отменим предыдущий
            if (_tasks.TryGetValue(sid, out var old))
            {
                old.Cancelled = true;
                _tasks.Remove(sid);
                try { old.OnCancel?.Invoke("replaced"); } catch { }
            }

            Vector3? startPos = null;
            if (!allowMove)
            {
                var pawn = caster.PlayerPawn?.Value;
                if (pawn is null || !pawn.IsValid || pawn.AbsOrigin is not { } org) return false;
                startPos = new Vector3(org.X, org.Y, org.Z);
            }

            var task = new CastTask
            {
                Plugin = plugin,
                Caster = caster,
                CasterSid = sid,
                SpellId = spellId,
                DurationSec = durationSec,
                StartUtc = DateTime.UtcNow,
                AllowMove = allowMove,
                MoveTolerance = moveTolerance,
                CancelOnStun = cancelOnStun,
                CancelOnSilence = cancelOnSilence,
                StartPos = startPos,
                OnComplete = onComplete,
                OnCancel = onCancel,
                Cancelled = false
            };

            _tasks[sid] = task;

            // цикл 10 Гц
            void Tick()
            {
                if (! _tasks.TryGetValue(sid, out var t) || t.Cancelled) return;

                try
                {
                    // валидность
                    var pl = t.Caster;
                    if (pl is null || !pl.IsValid)
                    {
                        CancelInternal(t, "invalid");
                        return;
                    }

                    // контроль
                    if (t.CancelOnStun && t.Plugin.WowControl.IsStunned(sid))
                    {
                        CancelInternal(t, "stunned");
                        return;
                    }
                    if (t.CancelOnSilence && t.Plugin.WowControl.IsSilenced(sid))
                    {
                        CancelInternal(t, "silenced");
                        return;
                    }

                    // движение
                    if (!t.AllowMove && t.StartPos is Vector3 s0)
                    {
                        var pawn = pl.PlayerPawn?.Value;
                        if (pawn is null || !pawn.IsValid || pawn.AbsOrigin is not { } org)
                        {
                            CancelInternal(t, "moved/invalid");
                            return;
                        }
                        var cur = new Vector3(org.X, org.Y, org.Z);
                        if ((cur - s0).Length() > t.MoveTolerance)
                        {
                            CancelInternal(t, "moved");
                            return;
                        }
                    }

                    // завершение
                    var elapsed = (DateTime.UtcNow - t.StartUtc).TotalSeconds;
                    if (elapsed >= t.DurationSec)
                    {
                        _tasks.Remove(sid);
                        try { t.OnComplete(); } catch { }
                        return;
                    }
                }
                catch
                {
                    CancelInternal(task, "error");
                    return;
                }

                // резкедул
                t.Plugin.AddTimer(0.1f, Tick);
            }

            task.Plugin.AddTimer(0.1f, Tick);
            return true;
        }

        public static void CancelFor(ulong casterSid, string reason = "cancelled")
        {
            if (_tasks.TryGetValue(casterSid, out var t))
            {
                CancelInternal(t, reason);
            }
        }

        public static void CancelAllFor(ulong casterSid) => CancelFor(casterSid);

        private static void CancelInternal(CastTask t, string reason)
        {
            if (!_tasks.ContainsKey(t.CasterSid)) return;
            t.Cancelled = true;
            _tasks.Remove(t.CasterSid);
            try { t.OnCancel?.Invoke(reason); } catch { }
        }
    }
}