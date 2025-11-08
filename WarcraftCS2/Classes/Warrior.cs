using System;
using System.Collections.Generic;
using WarcraftCS2.Gameplay;

namespace WarcraftCS2.Classes
{
    public sealed class Warrior : IWowClass
    {
        public string Id   => "warrior";
        public string Name => "Warrior";

        // 6 активных умений
        public IReadOnlyList<string> ActiveSpells => new[]
        {
            "warrior.whirlwind",
            "warrior.mortal_strike",
            "warrior.execute",
            "warrior.warbringer",
            "warrior.warcry",
            "warrior.bulwark",
        };

        // Встроенные таланты
        public IReadOnlyList<string> InnateTalents => new[]
        {
            "warrior.augments.unlock"
        };
    }
}
