using CounterStrikeSharp.API.Core;
using System;
using System.Collections.Generic;

namespace wowmod_cs2.MenuSystem
{
    internal class Menu
    {
        internal string Title { get; set; } = "";
        internal int ResultsBeforePaging { get; set; } = 5;
        internal LinkedList<MenuOption> Options { get; set; } = new();
        internal LinkedListNode<MenuOption>? Prev { get; set; } = null;

        internal LinkedListNode<MenuOption> Add(
            string display,
            string? subDisplay,
            Action<CCSPlayerController, MenuOption>? onChoose = null,
            Action<CCSPlayerController, MenuOption>? onSelect = null,
            bool enabled = true)
        {
            var newOption = new MenuOption
            {
                Parent = this,
                OptionDisplay = display,
                SubOptionDisplay = subDisplay ?? "",
                OnChoose = onChoose,
                OnSelect = onSelect,
                Enabled = enabled,
                Index = Options.Count
            };
            return Options.AddLast(newOption);
        }
    }
}