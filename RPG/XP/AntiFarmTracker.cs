using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace RPG.XP;

public static class AntiFarmTracker
{
    private readonly struct Pair : IEquatable<Pair>
    {
        public readonly ulong A; public readonly ulong B; public readonly bool IsHeal;
        public Pair(ulong a, ulong b, bool heal) { A = a; B = b; IsHeal = heal; }
        public bool Equals(Pair other) => A == other.A && B == other.B && IsHeal == other.IsHeal;
        public override bool Equals(object? obj) => obj is Pair p && Equals(p);
        public override int GetHashCode() => HashCode.Combine(A, B, IsHeal);
    }

    private sealed class StampQueue
    {
        public readonly Queue<double> Stamps = new();
        public readonly object Gate = new();
    }

    private static XpBalanceConfig _cfg = new();
    private static readonly ConcurrentDictionary<Pair, StampQueue> _pairs = new();

    public static void Init(XpBalanceConfig cfg)
    {
        _cfg = cfg;
        _pairs.Clear();
    }

    public static void ClearPlayer(ulong id)
    {
        foreach (var key in _pairs.Keys)
            if (key.A == id || key.B == id) _pairs.TryRemove(key, out _);
    }

    public static void RegisterDamage(ulong attackerId, ulong victimId, double nowSec)
        => RegisterInternal(new Pair(attackerId, victimId, heal: false), nowSec, _cfg.AntiFarm.DamageWindowSec);

    public static void RegisterHeal(ulong healerId, ulong targetId, double nowSec)
        => RegisterInternal(new Pair(healerId, targetId, heal: true), nowSec, _cfg.AntiFarm.HealWindowSec);

    private static void RegisterInternal(Pair key, double nowSec, int windowSec)
    {
        var sq = _pairs.GetOrAdd(key, _ => new StampQueue());
        lock (sq.Gate)
        {
            sq.Stamps.Enqueue(nowSec);
            var cutoff = nowSec - windowSec;
            while (sq.Stamps.Count > 0 && sq.Stamps.Peek() < cutoff)
                sq.Stamps.Dequeue();
        }
    }

    public static double AdjustPairFactor(ulong a, ulong b, double nowSec, bool damageLike)
    {
        var key = new Pair(a, b, heal: !damageLike);
        if (!_pairs.TryGetValue(key, out var sq)) return 1.0;

        int window = damageLike ? _cfg.AntiFarm.DamageWindowSec : _cfg.AntiFarm.HealWindowSec;
        int cap    = damageLike ? _cfg.AntiFarm.DamageSoftCap    : _cfg.AntiFarm.HealSoftCap;
        double k   = damageLike ? _cfg.AntiFarm.DamageDecayPerEvent : _cfg.AntiFarm.HealDecayPerEvent;
        double min = damageLike ? _cfg.AntiFarm.DamageMinFactor  : _cfg.AntiFarm.HealMinFactor;

        int count;
        lock (sq.Gate)
        {
            var cutoff = nowSec - window;
            while (sq.Stamps.Count > 0 && sq.Stamps.Peek() < cutoff)
                sq.Stamps.Dequeue();
            count = sq.Stamps.Count;
        }

        if (count <= cap) return 1.0;
        var over = count - cap;
        var factor = 1.0 - k * over;
        if (factor < min) factor = min;
        if (factor > 1.0) factor = 1.0;
        return factor;
    }
}