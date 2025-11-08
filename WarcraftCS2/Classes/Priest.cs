using System;
using System.Collections.Generic;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Data;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Status;
using RPG.XP;

namespace WarcraftCS2.Classes
{
    [WowRole(PlayerRole.Support)]
    public sealed class Priest : IWowClass
    {
        public string Id   => "priest";
        public string Name => "Priest";

        public IReadOnlyList<string> ActiveSpells => new[]
        {
            "priest.power_word_shield",
            "priest.flash_heal",
            "priest.prayer_of_healing",
            "priest.mind_blast",
            "priest.renew"
        };

        public IReadOnlyList<string> InnateTalents => Array.Empty<string>();
    }
}