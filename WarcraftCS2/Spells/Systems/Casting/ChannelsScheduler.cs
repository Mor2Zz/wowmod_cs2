using System;
using System.Collections.Generic;
using System.Numerics;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Timers;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Systems.Casting
{
    /// <summary>
    /// Планировщик каналов (channeled spells).
    /// BeginChannel(...) запускает тики по интервалу, автокансел по stun/silence/движению/инвалидности.
    /// </summary>
    public static class ChannelScheduler
    {
        private sealed class ChannelTask
        {
            public WowmodCs2 Plugin = null!;
            public CCSPlayerController Caster = null!;
            public ulong CasterSid;
            public string SpellId = "";
            public double TotalSec;
            public double TickSec;
            public DateTime StartUtc;
            public bool AllowMove;
            public float MoveTolerance;
            public bool CancelOnStun;
            public bool CancelOnSilence;
            public Vector3? StartPos;
            public Action OnTick = null!;
            public Action? OnEnd;
            public Action<string>? OnCancel;
            public bool Cancelled;
            public double Accumulator; // для точного тика
        }

        // casterSid -> active channel
        private static readonly Dictionary<ulong, ChannelTask> _tasks = new();

        public static bool IsChanneling(ulong casterSid) => _tasks.ContainsKey(casterSid);
        public static void CancelFor(ulong casterSid, string reason = "cancelled")
        {
            if (_tasks.TryGetValue(casterSid, out var t))
            {
                CancelInternal(t, reason);
            }
        }
        public static void CancelAllFor(ulong casterSid) => CancelFor(casterSid);

        public static bool BeginChannel(
            WowmodCs2 plugin,
            CCSPlayerController caster,
            string spellId,
            double totalDurationSec,
            double tickIntervalSec,
            Action onTick,
            Action? onEnd = null,
            Action<string>? onCancel = null,
            bool allowMove = false,
            float moveTolerance = 12f,
            bool cancelOnStun = true,
            bool cancelOnSilence = true)
        {
            if (plugin is null || caster is null || !caster.IsValid) return false;
            if (totalDurationSec <= 0 || tickIntervalSec <= 0 || onTick is null) return false;

            var sid = (ulong)caster.SteamID;

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

            var task = new ChannelTask
            {
                Plugin = plugin,
                Caster = caster,
                CasterSid = sid,
                SpellId = spellId,
                TotalSec = totalDurationSec,
                TickSec = tickIntervalSec,
                StartUtc = DateTime.UtcNow,
                AllowMove = allowMove,
                MoveTolerance = moveTolerance,
                CancelOnStun = cancelOnStun,
                CancelOnSilence = cancelOnSilence,
                StartPos = startPos,
                OnTick = onTick,
                OnEnd = onEnd,
                OnCancel = onCancel,
                Cancelled = false,
                Accumulator = 0
            };

            _tasks[sid] = task;

            void Tick()
            {
                if (!_tasks.TryGetValue(sid, out var t) || t.Cancelled) return;

                try
                {
                    var pl = t.Caster;
                    if (pl is null || !pl.IsValid) { CancelInternal(t, "invalid"); return; }

                    if (t.CancelOnStun && t.Plugin.WowControl.IsStunned(sid)) { CancelInternal(t, "stunned"); return; }
                    if (t.CancelOnSilence && t.Plugin.WowControl.IsSilenced(sid)) { CancelInternal(t, "silenced"); return; }

                    if (!t.AllowMove && t.StartPos is Vector3 s0)
                    {
                        var pawn = pl.PlayerPawn?.Value;
                        if (pawn is null || !pawn.IsValid || pawn.AbsOrigin is not { } org) { CancelInternal(t, "moved/invalid"); return; }
                        var cur = new Vector3(org.X, org.Y, org.Z);
                        if ((cur - s0).Length() > t.MoveTolerance) { CancelInternal(t, "moved"); return; }
                    }

                    var elapsed = (DateTime.UtcNow - t.StartUtc).TotalSeconds;
                    // обработка тиков
                    var nextAccumulator = t.Accumulator + 0.1;
                    while (nextAccumulator >= t.TickSec && elapsed < t.TotalSec)
                    {
                        nextAccumulator -= t.TickSec;
                        try { t.OnTick(); } catch { }
                        elapsed = (DateTime.UtcNow - t.StartUtc).TotalSeconds;
                    }
                    t.Accumulator = nextAccumulator;

                    if (elapsed >= t.TotalSec)
                    {
                        _tasks.Remove(sid);
                        try { t.OnEnd?.Invoke(); } catch { }
                        return;
                    }
                }
                catch
                {
                    CancelInternal(task, "error");
                    return;
                }

                t.Plugin.AddTimer(0.1f, Tick);
            }

            task.Plugin.AddTimer(0.1f, Tick);
            return true;
        }

        private static void CancelInternal(ChannelTask t, string reason)
        {
            if (!_tasks.ContainsKey(t.CasterSid)) return;
            t.Cancelled = true;
            _tasks.Remove(t.CasterSid);
            try { t.OnCancel?.Invoke(reason); } catch { }
        }
    }
}