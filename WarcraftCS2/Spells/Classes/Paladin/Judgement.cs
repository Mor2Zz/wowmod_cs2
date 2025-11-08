using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Data;
using WarcraftCS2.Spells.Systems.Core;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public class PaladinJudgement : IActiveSpell
    {
        public string Id => "paladin.judgement";
        public string Name => "Judgement";
        public string Description => "Правосудие (демо-удар).";

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (player is not { IsValid: true }) return false;
            var pawn = player.PlayerPawn?.Value;
            if (pawn is null) return false;

            int before = pawn.Health;
            int dmg = 10;
            int after = Math.Max(1, before - dmg);
            pawn.Health = after;

            rt.Print(player, $"[Warcraft] Judgement! HP: {before} → {after} (демо).");
            return true;
        }
    }
}