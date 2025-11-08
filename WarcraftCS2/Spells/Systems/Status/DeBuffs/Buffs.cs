using System;
using System.Collections.Generic;
using CounterStrikeSharp.API.Core;

namespace WarcraftCS2.Spells.Systems.Status;

    /// <summary>Реестр баффов/дебаффов с истечением по времени.</summary>
    public static class Buffs
    {
        private static readonly Dictionary<ulong, Dictionary<string, DateTime>> _buffs = new();

        // === Шаблоны для полной очистки (Cleanse) ===
        private static readonly string[] DefaultDebuffPrefixes =
        {
            "warrior.hamstring", "mage.frostbolt",
            "slow.", "snare.", "root.", "stun.", "silence.", "poison.",
            "bleed.", "ignite.", "fear.", "blind.", "disarm."
        };
        private static readonly string[] DefaultDebuffSubstrings =
        {
            "slow", "snare", "root", "stun", "silence", "poison",
            "bleed", "ignite", "fear", "blind", "disarm"
        };

        // === Только движение (Freedom) ===
        private static readonly string[] MovementDebuffPrefixes =
        {
            "warrior.hamstring", "mage.frostbolt", "slow.", "snare.", "root."
        };
        private static readonly string[] MovementDebuffSubstrings =
        {
            "slow", "snare", "root", "immobilize", "cripple"
        };

        public static void Add(ulong steamId, string buffId, TimeSpan duration)
        {
            if (string.IsNullOrWhiteSpace(buffId)) return;
            if (!_buffs.TryGetValue(steamId, out var map))
            {
                map = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                _buffs[steamId] = map;
            }
            if (duration <= TimeSpan.Zero) { Remove(steamId, buffId); return; }
            map[buffId] = DateTime.UtcNow.Add(duration);
        }

        public static bool Has(ulong steamId, string buffId)
        {
            if (!_buffs.TryGetValue(steamId, out var map)) return false;
            if (!map.TryGetValue(buffId, out var exp)) return false;
            if (exp <= DateTime.UtcNow)
            {
                map.Remove(buffId);
                if (map.Count == 0) _buffs.Remove(steamId);
                return false;
            }
            return true;
        }

        public static bool Remove(ulong steamId, string buffId)
        {
            if (!_buffs.TryGetValue(steamId, out var map)) return false;
            var removed = map.Remove(buffId);
            if (map.Count == 0) _buffs.Remove(steamId);
            return removed;
        }

        public static int RemoveByPrefix(ulong steamId, string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return 0;
            if (!_buffs.TryGetValue(steamId, out var map)) return 0;
            var keys = new List<string>();
            foreach (var k in map.Keys)
                if (k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) keys.Add(k);
            foreach (var k in keys) map.Remove(k);
            if (map.Count == 0) _buffs.Remove(steamId);
            return keys.Count;
        }

        public static int RemoveBySubstring(ulong steamId, string substring)
        {
            if (string.IsNullOrEmpty(substring)) return 0;
            if (!_buffs.TryGetValue(steamId, out var map)) return 0;
            var keys = new List<string>();
            foreach (var k in map.Keys)
                if (k.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0) keys.Add(k);
            foreach (var k in keys) map.Remove(k);
            if (map.Count == 0) _buffs.Remove(steamId);
            return keys.Count;
        }

        public static int CleanseAllDebuffs(ulong steamId,
            IEnumerable<string>? extraPrefixes = null,
            IEnumerable<string>? extraSubstrings = null)
        {
            int removed = 0;
            foreach (var p in DefaultDebuffPrefixes)   removed += RemoveByPrefix(steamId, p);
            foreach (var s in DefaultDebuffSubstrings) removed += RemoveBySubstring(steamId, s);
            if (extraPrefixes != null)   foreach (var p in extraPrefixes)   removed += RemoveByPrefix(steamId, p);
            if (extraSubstrings != null) foreach (var s in extraSubstrings) removed += RemoveBySubstring(steamId, s);
            return removed;
        }

        public static int CleanseMovementDebuffs(ulong steamId,
            IEnumerable<string>? extraPrefixes = null,
            IEnumerable<string>? extraSubstrings = null)
        {
            int removed = 0;
            foreach (var p in MovementDebuffPrefixes)   removed += RemoveByPrefix(steamId, p);
            foreach (var s in MovementDebuffSubstrings) removed += RemoveBySubstring(steamId, s);
            if (extraPrefixes != null)   foreach (var p in extraPrefixes)   removed += RemoveByPrefix(steamId, p);
            if (extraSubstrings != null) foreach (var s in extraSubstrings) removed += RemoveBySubstring(steamId, s);
            return removed;
        }

        public static void CleanupExpired(ulong steamId)
        {
            if (!_buffs.TryGetValue(steamId, out var map)) return;
            var now = DateTime.UtcNow;
            var toDel = new List<string>();
            foreach (var kv in map) if (kv.Value <= now) toDel.Add(kv.Key);
            foreach (var k in toDel) map.Remove(k);
            if (map.Count == 0) _buffs.Remove(steamId);
        }

        public static void ClearAll(ulong steamId) => _buffs.Remove(steamId);

        public static void OnPlayerDisconnected(CCSPlayerController player)
        {
            if (player == null || !player.IsValid) return;
            ClearAll(player.SteamID);
        }

        public static IReadOnlyCollection<string> GetActive(ulong steamId)
        {
            CleanupExpired(steamId);
            if (!_buffs.TryGetValue(steamId, out var map) || map.Count == 0)
                return Array.Empty<string>();
            return new List<string>(map.Keys);
        }
    }