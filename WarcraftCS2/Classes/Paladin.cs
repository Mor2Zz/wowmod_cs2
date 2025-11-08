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
    public sealed class Paladin : IWowClass
    {
        public string Id   => "paladin";
        public string Name => "Paladin";

       public IReadOnlyList<string> ActiveSpells => new[]
        {
            "paladin.heal",
            "paladin.divine_shield",
            "paladin.freedom",
            "paladin.judgement",
            "paladin.holy_shock",
            "paladin.lay_on_hands",
            "paladin.holy_radiance",
            "paladin.word_of_glory",
            "paladin.blessing_kings"
        };

        public IReadOnlyList<string> InnateTalents => Array.Empty<string>();
    }
}