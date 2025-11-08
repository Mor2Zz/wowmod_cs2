using System.Linq;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using wowmod_cs2.MenuSystem;

namespace wowmod_cs2
{
    public partial class WowmodCs2 : BasePlugin
    {
        private void OpenTalentsMenu(CCSPlayerController player)
        {
            var prof = GetOrCreateProfile(player);
            if (!WowRegistry.Classes.TryGetValue(prof.ClassId, out var cls))
            {
                player.PrintToChat("[wowmod] Select a class first.");
                OpenRootMenu(player);
                return;
            }

            var menu = MenuSystem.MenuManager.CreateMenu($"Talents — {cls.Name}", 5);

            var available = WowRegistry.Talents.Values
                .Where(t => t.ClassId == cls.Id && prof.Level >= t.MinLevel)
                .OrderBy(t => t.MinLevel).ThenBy(t => t.Name)
                .ToArray();

            if (available.Length == 0)
            {
                menu.Add("No available talents", null, (p, opt) => OpenRootMenu(p));
            }
            else
            {
                foreach (var t in available)
                {
                    var localT = t;
                    menu.Add($"{t.Name} (req {t.MinLevel})", null, (p, opt) =>
                    {
                        p.PrintToChat($"[wowmod] {localT.Name}: {localT.Description}");
                        OpenTalentsMenu(p);
                    });
                }
            }

            menu.Add("↩ Back", null, (p, opt) => OpenRootMenu(p));
            MenuSystem.MenuManager.OpenMainMenu(player, menu);
        }
    }
}
