using System;
using System.Collections.Generic;

namespace WarcraftCS2.Spells.Systems.Core.Runtime
{
    // PRNG с «защитой от невезения» + RPPM + ICD, хранение состояния по (sid,key)
    public sealed class ProcRandom
    {
        private readonly Random _rng;
        private readonly Dictionary<(ulong sid, string key), State> _map = new();

        private struct State
        {
            public int FailStreak;
            public double LastProcTime; // в секундах, для RPPM/ICD
        }

        public ProcRandom(int? seed = null)
        {
            _rng = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        // Бернулли с Bad-Luck Protection: p = clamp01(base + FailStreak * bonus), cap maxChance
        public bool TestBernoulliBlp(ulong sid, string key, float baseChance01, float bonusPerFail01 = 0.03f, float maxChance01 = 0.95f)
        {
            var k = (sid, key);
            if (!_map.TryGetValue(k, out var s)) s = default;

            var p = Clamp01(baseChance01 + s.FailStreak * bonusPerFail01);
            if (p > maxChance01) p = maxChance01;

            var ok = _rng.NextDouble() < p;

            if (ok) s.FailStreak = 0;
            else s.FailStreak = Math.Min(s.FailStreak + 1, 1000000);

            _map[k] = s;
            return ok;
        }

        // RPPM: вероятность на dt — p = rppm * haste * dt / 60 (без учёта «ускоряющего» чина, логика простая и быстрая)
        public bool TestRppm(ulong sid, string key, float rppm, double nowSeconds, float hasteMult = 1f)
        {
            var k = (sid, key);
            if (!_map.TryGetValue(k, out var s)) s = default;

            var dt = s.LastProcTime <= 0 ? 0.0 : (nowSeconds - s.LastProcTime);
            var p = rppm * Math.Max(0.0, dt) * Math.Max(0.0f, hasteMult) / 60.0;
            if (p > 0.99) p = 0.99;

            var ok = _rng.NextDouble() < p;
            if (ok) s.LastProcTime = nowSeconds;

            _map[k] = s;
            return ok;
        }

        // ICD: внутренний КД в секундах — true, если КД вышел и мы его "зажигаем" сейчас
        public bool TestIcd(ulong sid, string key, double nowSeconds, double icdSeconds)
        {
            var k = (sid, key);
            if (!_map.TryGetValue(k, out var s)) s = default;

            var dt = s.LastProcTime <= 0 ? double.MaxValue : (nowSeconds - s.LastProcTime);
            if (dt >= icdSeconds)
            {
                s.LastProcTime = nowSeconds;
                _map[k] = s;
                return true;
            }

            _map[k] = s;
            return false;
        }

        // Остаток КД в секундах (0, если готово)
        public double CooldownRemaining(ulong sid, string key, double nowSeconds, double icdSeconds)
        {
            var k = (sid, key);
            if (!_map.TryGetValue(k, out var s) || s.LastProcTime <= 0) return 0.0;
            var dt = nowSeconds - s.LastProcTime;
            var rem = icdSeconds - dt;
            return rem > 0 ? rem : 0.0;
        }

        // Сброс состояния
        public void ResetAll() => _map.Clear();

        public void ResetKey(ulong sid, string key)
        {
            _map.Remove((sid, key));
        }

        public void ResetSid(ulong sid)
        {
            var tmp = new List<(ulong sid, string key)>();
            foreach (var e in _map.Keys)
                if (e.sid == sid) tmp.Add(e);
            foreach (var e in tmp) _map.Remove(e);
        }

        // Отладка/диагностика
        public int GetFailStreak(ulong sid, string key)
        {
            return _map.TryGetValue((sid, key), out var s) ? s.FailStreak : 0;
        }

        public double GetLastProcTime(ulong sid, string key)
        {
            return _map.TryGetValue((sid, key), out var s) ? s.LastProcTime : 0.0;
        }

        private static float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
    }
}