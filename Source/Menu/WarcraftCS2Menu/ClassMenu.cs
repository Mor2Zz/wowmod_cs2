using System.Linq;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using wowmod_cs2.MenuSystem;
using WarcraftCS2.Spells.Systems.Data;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Status;

namespace wowmod_cs2
{
    public partial class WowmodCs2 : BasePlugin
    {
        private void OpenClassMenu(CCSPlayerController player)
        {
            var menu = MenuSystem.MenuManager.CreateMenu("Classes", 5);

            foreach (var cls in WowRegistry.Classes.Values.OrderBy(c => c.Name))
            {
                var title = $"[{cls.Id}] {cls.Name}";
                var localCls = cls;
                menu.Add(title, null, (p, opt) =>
                {
                    var prof = GetOrCreateProfile(p);
                    prof.ClassId = localCls.Id;
                    SaveProfiles();
                    p.PrintToChat($"[wowmod] Class set: {localCls.Name}");
                    OpenRootMenu(p);
                });
            }

            menu.Add("↩ Back", null, (p, opt) => OpenRootMenu(p));
            MenuSystem.MenuManager.OpenMainMenu(player, menu);
        }
    }
}
