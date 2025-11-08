using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Paladin
{
    /// Holy Radiance: AoE хил союзников вокруг паладина.
    public class PaladinHolyRadiance : IActiveSpell
    {
        public string Id => "paladin.holy_radiance";
        public string Name => "Holy Radiance";
        public string Description => "Лечит союзников в радиусе 280 на 22 HP.";

        private const double ManaCost     = 24.0;
        private const double CooldownSec  = 12.0;
        private const float  Radius       = 280f;
        private const int    HealAmount   = 22;

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (player is not { IsValid: true }) return false;
            if (rt is not WowmodCs2 plugin) return false;

            var sid = (ulong)player.SteamID;
            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, Id, ManaCost, CooldownSec, out var reason))
            { rt.Print(player, $"[Warcraft] Holy Radiance: {reason}."); return false; }

            var pawn = player.PlayerPawn?.Value;
            if (pawn is not { IsValid: true, AbsOrigin: { } origin }) return false;

            int myTeam = Convert.ToInt32(player.Team);
            int healed = 0;
            foreach (var p in Utilities.GetPlayers())
            {
                if (p is null || !p.IsValid) continue;
                if (Convert.ToInt32(p.Team) != myTeam) continue;
                var pp = p.PlayerPawn?.Value;
                if (pp is not { IsValid: true, AbsOrigin: { } o2 }) continue;

                var dx = o2.X - origin.X; var dy = o2.Y - origin.Y; var dz = o2.Z - origin.Z;
                var d2 = dx*dx + dy*dy + dz*dz;
                if (d2 > Radius * Radius) continue;

                int before = pp.Health;
                int after  = Math.Clamp(before + HealAmount, 1, 120);
                if (after > before)
                {
                    pp.Health = after;
                    healed++;
                }
            }

            rt.Print(player, healed > 0
                ? $"[Warcraft] Holy Radiance: исцелено союзников: {healed}."
                : "[Warcraft] Holy Radiance: рядом нет союзников.");
            return true;
        }
    }
}