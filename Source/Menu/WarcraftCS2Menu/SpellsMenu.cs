using System.Linq;
using CounterStrikeSharp.API.Core;
using wowmod_cs2.MenuSystem;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Data;
using wowmod_cs2.Features;

namespace wowmod_cs2
{
    public partial class WowmodCs2 : BasePlugin
    {
        private void OpenSpellsMenu(CCSPlayerController player)
        {
            var prof = GetOrCreateProfile(player);
            if (!WowRegistry.Classes.TryGetValue(prof.ClassId, out var cls))
            { player.PrintToChat("[wowmod] Сначала выберите класс."); OpenRootMenu(player); return; }

            var abilityTitle  = FormatTitle(prof.Ability);
            var ultimateTitle = FormatTitle(prof.Ultimate);

            var menu = MenuManager.CreateMenu("Заклинания", 7);

            menu.Add($"Ability: {abilityTitle}",  null, (p, _) => OpenSpellPickMenu(p, isUltimate: false));
            menu.Add($"Ultimate: {ultimateTitle}", null, (p, _) => OpenSpellPickMenu(p, isUltimate: true));

            if (prof.ClassId == "warrior" && Augments.UnlockedForClass(prof, "warrior"))
                menu.Add("Модификаторы умений", "Настройка аугментов для 6 умений Воина.", (p, _) => OpenAugmentsRootMenu(p));

            menu.Add("↩ Назад", null, (p, _) => OpenRootMenu(p));
            MenuManager.OpenMainMenu(player, menu);
        }

        private void OpenSpellPickMenu(CCSPlayerController player, bool isUltimate)
        {
            var prof = GetOrCreateProfile(player);
            if (!WowRegistry.Classes.TryGetValue(prof.ClassId, out var cls))
            { player.PrintToChat("[wowmod] Сначала выберите класс."); OpenRootMenu(player); return; }

            var menu = MenuManager.CreateMenu(isUltimate ? "Выбор Ultimate" : "Выбор Ability", 7);

            foreach (var sid in cls.ActiveSpells)
            {
                if (!WowRegistry.Spells.TryGetValue(sid, out var sp)) continue;
                var title = SpellTitlesRu.GetTitle(sid, sp.Name);

                string? desc = null;
                if (SpellDescriptions.TryGet(sid, out var d) && !string.IsNullOrWhiteSpace(d)) desc = d;

                var localSid = sid;
                menu.Add(title, desc, (p, _) =>
                {
                    if (isUltimate) prof.Ultimate = localSid; else prof.Ability = localSid;
                    SaveProfilesSafe();
                    p.PrintToChat($"[wowmod] {(isUltimate ? "Ultimate" : "Ability")} назначен: {title}");

                    // после выбора — назад в меню спеллов
                    OpenSpellsMenu(p);
                });
            }

            menu.Add("↩ Назад", null, (p, _) => OpenSpellsMenu(p));
            MenuManager.OpenSubMenu(player, menu);
        }

        private static string FormatTitle(string? id)
            => string.IsNullOrWhiteSpace(id) ? "—" :
               (WowRegistry.Spells.TryGetValue(id!, out var sp) ? SpellTitlesRu.GetTitle(id!, sp.Name) : id!);

        // ==== ФИКС: совместимость со старым вызовом ====
        // Старый код вызывает OpenAugmentsRootMenu(p); оставляем ту же сигнатуру.
        private void OpenAugmentsRootMenu(CCSPlayerController player)
        {
            // зовём новое меню аугментов (фильтрует только экипнутые Ability/Ultimate)
            WarcraftCS2.Menu.AugmentsMenu.Open(this, player);
        }
    }
}