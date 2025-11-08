using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Data;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public class PaladinHeal : IActiveSpell
    {
        public string Id => "paladin.heal";
        public string Name => "Свет";
        public string Description => "Мгновенно лечит на ~35 HP (не выше 120).";

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (player is not { IsValid: true }) return false;
            var pawn = player.PlayerPawn?.Value;
            if (pawn is null) return false;

            int before = pawn.Health;
            int after  = Math.Clamp(before + 35, 1, 120);
            pawn.Health = after;

            rt.Print(player, $"[Warcraft] Свет исцеляет: {before} → {after} HP");
            return true;
        }
    }
}