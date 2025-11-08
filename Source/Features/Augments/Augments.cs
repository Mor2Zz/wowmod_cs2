using System;
using System.Collections.Generic;
using System.Linq;
using WarcraftCS2.Gameplay;

namespace wowmod_cs2.Features
{
    /// <summary>
    /// Хелперы для аугментов (модификаторов спеллов).
    /// Хранение — в PlayerProfile.Talents (HashSet<string>).
    /// </summary>
    public static class Augments
    {
        // Тег-разрешение для конкретного класса: "<class>.augments_unlocked"
        public static string UnlockTag(string classId) => $"{classId}.augments_unlocked";

        public static bool UnlockedForClass(PlayerProfile p, string classId)
            => p.Talents != null && p.Talents.Contains(UnlockTag(classId));

        public static void GrantUnlockForClass(PlayerProfile p, string classId)
        {
            p.Talents ??= new HashSet<string>();
            p.Talents.Add(UnlockTag(classId));
        }

        public static void RevokeUnlockForClass(PlayerProfile p, string classId)
            => p.Talents?.Remove(UnlockTag(classId));

        // выбор аугмента для конкретного спелла: "aug.<spellId>:<key>"
        private static string MakeKey(string spellId, string key) => $"aug.{spellId}:{key}";
        private static bool IsAugKey(string s, string spellId) => s.StartsWith($"aug.{spellId}:", StringComparison.Ordinal);

        public static string? GetSelected(PlayerProfile p, string spellId)
        {
            if (p.Talents == null) return null;
            var hit = p.Talents.FirstOrDefault(s => IsAugKey(s, spellId));
            if (hit == null) return null;
            var idx = hit.IndexOf(':');
            return (idx > 0 && idx < hit.Length - 1) ? hit[(idx + 1)..] : null;
        }

        public static bool IsSelected(PlayerProfile p, string spellId, string key)
            => GetSelected(p, spellId) == key;

        public static void Select(PlayerProfile p, string spellId, string key)
        {
            p.Talents ??= new HashSet<string>();
            p.Talents.RemoveWhere(s => IsAugKey(s, spellId));
            p.Talents.Add(MakeKey(spellId, key));
        }

        public static void Clear(PlayerProfile p, string spellId)
            => p.Talents?.RemoveWhere(s => IsAugKey(s, spellId));
    }
}