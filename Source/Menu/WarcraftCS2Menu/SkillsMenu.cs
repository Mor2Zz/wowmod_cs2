using CounterStrikeSharp.API.Core;
using wowmod_cs2.MenuSystem;

namespace wowmod_cs2
{
    public partial class WowmodCs2 : BasePlugin
    {
        private void OpenSkillsMenu(CCSPlayerController player)
        {
            var menu = MenuManager.CreateMenu("Skills", 5);
            menu.Add("Coming soon…", null, (p, _) => OpenRootMenu(p));
            menu.Add("↩ Back",       null, (p, _) => OpenRootMenu(p));
            MenuManager.OpenMainMenu(player, menu);
        }
    }
}