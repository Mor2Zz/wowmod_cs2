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
    public sealed class Warlock : IWowClass
    {
        public string Id   => "warlock";
        public string Name => "Warlock";

        public IReadOnlyList<string> ActiveSpells => new[]
        {
            "warlock.shadow_bolt",
            "warlock.corruption",
            "warlock.fear"
        };

        public IReadOnlyList<string> InnateTalents => Array.Empty<string>();
    }
}