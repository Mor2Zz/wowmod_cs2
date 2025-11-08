using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    /// Word of Glory: мгновенный сильный хил союзника (или себя).
    public class PaladinWordOfGlory : IActiveSpell
    {
        public string Id => "paladin.word_of_glory";
        public string Name => "Word of Glory";
        public string Description => "Мгновенно лечит цель на 40 HP (не выше 120).";

        private const double ManaCost     = 28.0;
        private const double CooldownSec  = 10.0;
        private const int    HealAmount   = 40;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (player is not { IsValid: true }) return false;

            var target = Targeting.TraceAllyByView(player) ?? player;
            var pawn = target.PlayerPawn?.Value;
            if (pawn is null) return false;

            int before = pawn.Health;
            int after  = Math.Clamp(before + HealAmount, 1, 120);
            pawn.Health = after;

            rt.Print(player, target == player
                ? $"[Warcraft] Word of Glory: +{after - before} HP (self)."
                : $"[Warcraft] Word of Glory: {after - before} HP → союзнику.");

            return true;
        }
    }
}
