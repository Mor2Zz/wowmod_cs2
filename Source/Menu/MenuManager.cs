using CounterStrikeSharp.API.Core;

namespace wowmod_cs2.MenuSystem
{
    internal static class MenuManager
    {
        internal static void OpenMainMenu(CCSPlayerController player, Menu menu, int selectedOptionIndex = 0)
        {
            if (player == null) return;
            MenuAPI.GetPlayer(player).OpenMainMenu(menu, selectedOptionIndex);
        }

        internal static void OpenSubMenu(CCSPlayerController player, Menu menu)
        {
            if (player == null) return;
            MenuAPI.GetPlayer(player).OpenSubMenu(menu);
        }

        internal static void CloseAllSubMenus(CCSPlayerController player)
        {
            if (player == null) return;
            MenuAPI.GetPlayer(player).CloseAllSubMenus();
        }

        internal static Menu CreateMenu(string title = "", int resultsBeforePaging = 5)
        {
            return new Menu { Title = title, ResultsBeforePaging = resultsBeforePaging };
        }
    }
}
