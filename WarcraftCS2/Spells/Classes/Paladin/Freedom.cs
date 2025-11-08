using System;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Status; // Buffs
using WarcraftCS2.Spells.Systems.Data;
using WarcraftCS2.Spells.Systems.Core;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    public class PaladinFreedom : IActiveSpell
    {
        public string Id => "paladin.freedom";
        public string Name => "Freedom";
        public string Description => "Снимает эффекты движения (slow/snare/root) и даёт к ним иммун 3с.";

        private static readonly TimeSpan Immunity = TimeSpan.FromSeconds(3);

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (player is not { IsValid: true }) return false;

            int removed = Buffs.CleanseMovementDebuffs(player.SteamID);

            Buffs.Add(player.SteamID, "paladin.freedom.immune_slow",  Immunity);
            Buffs.Add(player.SteamID, "paladin.freedom.immune_snare", Immunity);
            Buffs.Add(player.SteamID, "paladin.freedom.immune_root",  Immunity);

            rt.Print(player, $"[Warcraft] Freedom: снято эффектов движения: {removed}, иммун 3с.");
            return true;
        }
    }
}