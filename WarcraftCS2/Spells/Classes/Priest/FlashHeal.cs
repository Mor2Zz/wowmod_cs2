using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Classes.Priest
{
    public class PriestFlashHeal : IActiveSpell
    {
        public string Id => "priest.flash_heal";
        public string Name => "Flash Heal";
        public string Description => "Мгновенно лечит союзника на 35 HP.";

        private const double ManaCost = 22.0;
        private const double CooldownSec = 6.0;
        private const int HealAmount = 35;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (player is not { IsValid: true }) return false;
            var target = Targeting.TraceAllyByView(player) ?? player;

            var pawn = target.PlayerPawn?.Value;
            if (pawn is null) return false;

            int before = pawn.Health;
            int after  = Math.Clamp(before + HealAmount, 1, 120);
            pawn.Health = after;

            rt.Print(player, $"[Warcraft] Flash Heal: +{after - before} HP.");
            return true;
        }
    }
}