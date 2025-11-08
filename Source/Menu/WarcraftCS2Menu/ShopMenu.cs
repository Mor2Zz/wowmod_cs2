using CounterStrikeSharp.API.Core;
using wowmod_cs2.MenuSystem;

namespace wowmod_cs2
{
    public partial class WowmodCs2 : BasePlugin
    {
        private void OpenShopMenu(CCSPlayerController player)
        {
            var menu = MenuManager.CreateMenu("Shop", 5);
            menu.Add("Coming later…", null, (p, _) => OpenRootMenu(p));
            menu.Add("↩ Back",        null, (p, _) => OpenRootMenu(p));
            MenuManager.OpenMainMenu(player, menu);
        }
    }
}