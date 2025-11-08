using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Status; // Buffs
using WarcraftCS2.Spells.Systems.Data;
using WarcraftCS2.Spells.Systems.Core;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public class PaladinDivineShield : IActiveSpell
    {
        public string Id => "paladin.divine_shield";
        public string Name => "Божественный щит";
        public string Description => "Иммунитет к урону на 3 секунды.";

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (player is not { IsValid: true }) return false;
            Buffs.Add(player.SteamID, Id, TimeSpan.FromSeconds(3));
            rt.Print(player, "[Warcraft] Вы под Божественным щитом (3с)!");
            return true;
        }
    }
}