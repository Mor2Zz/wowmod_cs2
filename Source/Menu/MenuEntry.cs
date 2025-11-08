using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using wowmod_cs2.MenuSystem;

namespace wowmod_cs2
{
    public partial class WowmodCs2 : BasePlugin
    {
        private void InitWowMenu()
        {
            Logger?.LogInformation("[wowmod] Меню инициализировано (кастом W/S/E/R)");
            MenuAPI.Load(this);

            TryAddCommand("wow", "Открыть меню Warcraft", CmdWow);
            TryAddCommand("wcs", "Открыть меню Warcraft", CmdWow);
            TryAddCommand("цсы", "Открыть меню Warcraft", CmdWow);
        }

        private void TryAddCommand(string name, string help, CommandInfo.CommandCallback cb)
        {
            try { AddCommand(name, help, cb); } catch {}
        }

        private void CmdWow(CCSPlayerController? player, CommandInfo _)
        {
            if (player is null || !player.IsValid) return;
            OpenRootMenu(player);
        }

        private void OpenRootMenu(CCSPlayerController player)
        {
            var menu = MenuManager.CreateMenu("Warcraft — главное меню", 5);
            menu.Add("Классы",      null, (p, _) => OpenClassMenu(p));
            menu.Add("Навыки",      null, (p, _) => OpenSkillsMenu(p));
            menu.Add("Заклинания",  null, (p, _) => OpenSpellsMenu(p));
            menu.Add("Таланты",     null, (p, _) => OpenTalentsMenu(p));
            // menu.Add("Магазин",  null, (p, _) => OpenShopMenu(p)); // позже
            MenuManager.OpenMainMenu(player, menu);
        }
    }
}
