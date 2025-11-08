using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using wowmod_cs2;
using WarcraftCS2.Gameplay;
using wowmod_cs2.Features; // Augments

namespace WarcraftCS2.Menu
{
    // Показывает аугменты только для экипнутых спеллов (Ability + Ultimate).
    public static class AugmentsMenu
    {
        // Совместимость со старым вызовом из SpellsMenu
        public static void OpenAugmentsRootMenu(WowmodCs2 plugin, CCSPlayerController player) => Open(plugin, player);

        // Точка входа
        public static void Open(WowmodCs2 plugin, CCSPlayerController player)
        {
            var prof = plugin.GetProfile(player);
            if (prof == null)
            {
                player.PrintToChat("[Warcraft] Профиль не найден.");
                return;
            }

            var classId = (prof.ClassId ?? "warrior").ToLowerInvariant();
            var known = KnownByClass(classId);

            var equipped = GetEquippedSpellIds(prof);
            if (equipped.Count == 0)
            {
                player.PrintToChat("[Warcraft] Сначала выбери умения в меню Spells (Ability и Ultimate).");
                return;
            }

            var toShow = equipped.Where(s => known.Contains(s, StringComparer.OrdinalIgnoreCase)).ToList();
            if (toShow.Count == 0)
            {
                player.PrintToChat("[Warcraft] Для экипнутых умений нет аугментов.");
                return;
            }

            foreach (var spellId in toShow)
            {
                RenderSpellAugments(plugin, player, prof, spellId);
            }
        }

        // --- Утилиты ---

        private static HashSet<string> KnownByClass(string classId)
        {
            if (classId == "warrior")
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "warrior.whirlwind",
                    "warrior.mortal_strike",
                    "warrior.execute",
                    "warrior.warbringer",
                    "warrior.warcry",
                    "warrior.bulwark"
                };
            }

            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private static HashSet<string> GetEquippedSpellIds(object profile)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string? ability  = FindSpellInProfile(profile, "ability");
            string? ultimate = FindSpellInProfile(profile, "ultimate");

            if (!string.IsNullOrWhiteSpace(ability))  result.Add(NormalizeSpellId(ability!));
            if (!string.IsNullOrWhiteSpace(ultimate)) result.Add(NormalizeSpellId(ultimate!));

            return result;
        }

        private static string NormalizeSpellId(string raw)
        {
            return raw.Trim().Replace(' ', '_').ToLowerInvariant();
        }

        private static string? FindSpellInProfile(object root, string slotKey)
        {
            if (root == null) return null;
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            return FindRecursive(root, slotKey, visited);
        }

        private static string? FindRecursive(object obj, string slotKey, HashSet<object> visited)
        {
            if (obj == null) return null;
            if (!visited.Add(obj)) return null;

            var t = obj.GetType();

            var direct = TryGetStringMembers(obj, t, slotKey);
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            foreach (var name in new[] { "Loadout", "Selected", "Spellbar", "Spells", "Binds", "Equip", "Selection" })
            {
                var nested = TryGetObjectMember(obj, t, name);
                if (nested != null)
                {
                    var val = FindRecursive(nested, slotKey, visited);
                    if (!string.IsNullOrWhiteSpace(val)) return val;
                }
            }

            return null;
        }

        private static string? TryGetStringMembers(object obj, Type t, string slotKey)
        {
            foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (p.PropertyType != typeof(string)) continue;
                var name = p.Name.ToLowerInvariant();
                if (!IsSlotNameMatch(name, slotKey)) continue;

                try
                {
                    var val = p.GetValue(obj) as string;
                    if (!string.IsNullOrWhiteSpace(val)) return val!;
                }
                catch { }
            }

            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (f.FieldType != typeof(string)) continue;
                var name = f.Name.ToLowerInvariant();
                if (!IsSlotNameMatch(name, slotKey)) continue;

                try
                {
                    var val = f.GetValue(obj) as string;
                    if (!string.IsNullOrWhiteSpace(val)) return val!;
                }
                catch { }
            }

            return null;
        }

        private static object? TryGetObjectMember(object obj, Type t, string nameLike)
        {
            var p = t.GetProperty(nameLike, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                           | BindingFlags.IgnoreCase);
            if (p != null && p.PropertyType != typeof(string))
            {
                try { return p.GetValue(obj); } catch { }
            }

            var f = t.GetField(nameLike, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                        | BindingFlags.IgnoreCase);
            if (f != null && f.FieldType != typeof(string))
            {
                try { return f.GetValue(obj); } catch { }
            }

            return null;
        }

        private static bool IsSlotNameMatch(string propLower, string slotKeyLower)
        {
            if (propLower.Contains(slotKeyLower)) return true;

            if (slotKeyLower == "ultimate" && (propLower.EndsWith(".r") || propLower.Contains("ult"))) return true;
            if (slotKeyLower == "ability"  && (propLower.EndsWith(".e") || propLower.Contains("abil"))) return true;

            return false;
        }

        private static void RenderSpellAugments(WowmodCs2 plugin, CCSPlayerController player, dynamic prof, string spellId)
        {
            var selected = Augments.GetSelected(prof, spellId) ?? "(none)";
            var options = GetAugmentOptionsForSpell(spellId);

            // тут может быть твой HTML/меню; для самодостаточности — вывод в чат
            player.PrintToChat($"[Augments] {spellId}: выбран = {selected}");
            foreach (var (key, title) in options)
            {
                player.PrintToChat($"  • {title}  (/aug {spellId} {key})");
            }
        }

        private static List<(string key, string title)> GetAugmentOptionsForSpell(string spellId)
        {
            var list = new List<(string, string)>();
            switch (spellId.ToLowerInvariant())
            {
                case "warrior.whirlwind":
                    list.Add(("bladestorm", "Bladestorm (−урон тиком; иммюн к софт-CC — если включишь)"));
                    list.Add(("bloodstorm", "Bloodstorm (bleed + leech)"));
                    list.Add(("razor_guard", "Razor Guard (щит за каждую цель)"));
                    break;

                case "warrior.mortal_strike":
                    list.Add(("trauma", "Trauma (+антихил, −броня цели на короткое время)"));
                    list.Add(("ex_precision", "Ex. Precision (усиленный bleed; при килле → возврат КД)"));
                    list.Add(("defiance", "Defiance (−урон удара; защитный бонус на кастере)"));
                    break;

                case "warrior.execute":
                    list.Add(("guillotine", "Guillotine (+урон, можно сделать кастовый)"));
                    list.Add(("mercy_is_weakness", "Mercy Is Weakness (+порог, −база)"));
                    list.Add(("deathmark", "Deathmark (метка после удара)"));
                    break;

                case "warrior.warbringer":
                    list.Add(("skullcracker", "Skullcracker (+стан, деморайз)"));
                    list.Add(("earthsplit", "Earthsplit (волны урона вокруг цели)"));
                    list.Add(("warpath", "Warpath (частичный возврат КД при группе)"));
                    break;

                case "warrior.warcry":
                    list.Add(("terrifying_bellow", "Terrifying Bellow (мини-фир)"));
                    list.Add(("commanding_shout", "Commanding Shout (+длительность деморайза)"));
                    list.Add(("echo", "Echo (отложенный фир+деморайз)"));
                    break;

                case "warrior.bulwark":
                    list.Add(("spellbreaker", "Spellbreaker (короче, но плотнее/маг-фокус)"));
                    list.Add(("phalanx", "Phalanx (союзный щит рядом)"));
                    list.Add(("riposte", "Riposte (мини-контратака)"));
                    break;

                default:
                    break;
            }
            return list;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

            // new — чтобы явно скрыть статический object.Equals(object?, object?)
            public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

            public int GetHashCode(object obj) => obj is null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}