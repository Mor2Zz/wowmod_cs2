using System;
using System.Collections.Generic;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Data;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Status;
using RPG.XP; 

namespace WarcraftCS2.Classes
{
    [WowRole(PlayerRole.Dps)] 
    public sealed class Mage : IWowClass
    {
        public string Id   => "mage";
        public string Name => "Mage";

        public IReadOnlyList<string> ActiveSpells => new[]
        {
            "mage.blink",
            "mage.frostbolt",
            "mage.ice_block",
            "mage.arcane_missiles",
            "mage.arcane_explosion",
            "mage.mana_shield"
        };
        
        public IReadOnlyList<string> InnateTalents => Array.Empty<string>();
    }
}