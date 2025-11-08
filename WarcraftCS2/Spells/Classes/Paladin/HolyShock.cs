using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Data;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public class PaladinHolyShock : IActiveSpell
    {
        public string Id => "paladin.holy_shock";
        public string Name => "Holy Shock";
        public string Description => "Лёгкий мгновенный хил.";

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (player is not { IsValid: true }) return false;
            var pawn = player.PlayerPawn?.Value;
            if (pawn is null) return false;

            int before = pawn.Health;
            int after  = Math.Clamp(before + 20, 1, 120);
            pawn.Health = after;

            rt.Print(player, $"[Warcraft] Holy Shock! HP: {before} → {after}.");
            return true;
        }
    }
}