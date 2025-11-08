using CounterStrikeSharp.API.Core;
using System;

namespace wowmod_cs2.MenuSystem
{
    internal class MenuOption
    {
        internal Menu Parent { get; set; } = null!;
        internal string OptionDisplay { get; set; } = "";
        internal string SubOptionDisplay { get; set; } = "";
        internal Action<CCSPlayerController, MenuOption>? OnChoose { get; set; }
        internal Action<CCSPlayerController, MenuOption>? OnSelect { get; set; }
        internal bool Enabled { get; set; } = true;
        internal int Index { get; set; }
    }
}
