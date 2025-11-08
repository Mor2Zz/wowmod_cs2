using System;
using System.Collections.Generic;
using RPG.XP;
using WarcraftCS2.Gameplay;

namespace WarcraftCS2.Classes
{
    [WowRole(PlayerRole.Dps)]
    public sealed class Shaman : IWowClass
    {
        public string Id   => "shaman";
        public string Name => "Shaman";

        public IReadOnlyList<string> ActiveSpells => new[]
        {
            "shaman.lightning_bolt",
            "shaman.flame_shock",
            "shaman.frost_shock",
            "shaman.windfury_weapon"
        };

        public IReadOnlyList<string> InnateTalents => Array.Empty<string>();
    }
}