using System;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Gameplay;
using WarcraftCS2.Spells.Systems.Casting;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using wowmod_cs2;

namespace WarcraftCS2.Spells.Classes.Priest
{
    public static class PrayerOfHealing
    {
        private const string SpellId      = "priest_prayer_of_healing";
        private const double ManaCost     = 28.0;
        private const double CooldownSec  = 12.0;
        private const int    HealAmount   = 25;
        private const float  Radius       = 300f;

        public static bool TryCast(WowmodCs2 plugin, CCSPlayerController caster, out string failReason)
        {
            failReason = string.Empty;
            if (plugin is null || caster is null || !caster.IsValid) { failReason = "Некорректный кастер"; return false; }

            var sid = (ulong)caster.SteamID;
            if (plugin.WowControl.IsStunned(sid))  { failReason = "Вы оглушены"; return false; }
            if (plugin.WowControl.IsSilenced(sid)) { failReason = "Вы немые";    return false; }

            var ctx = plugin.GetWowCombatContext();
            if (!CastGate.TryBeginCast(ctx, sid, SpellId, ManaCost, CooldownSec, out failReason))
                return false;

            var pawn = caster.PlayerPawn?.Value;
            if (pawn is not { IsValid: true, AbsOrigin: { } origin }) { failReason = "Нет позиции"; return false; }

            int myTeam = (int)caster.Team;
            int healed = 0;

            foreach (var p in Utilities.GetPlayers())
            {
                if (p is null || !p.IsValid || (int)p.Team != myTeam) continue;
                var pp = p.PlayerPawn?.Value;
                if (pp is not { IsValid: true, AbsOrigin: { } o2 }) continue;

                var dx = o2.X - origin.X; var dy = o2.Y - origin.Y; var dz = o2.Z - origin.Z;
                var d2 = dx * dx + dy * dy + dz * dz;
                if (d2 > Radius * Radius) continue;

                int before = pp.Health;
                int after  = Math.Clamp(before + HealAmount, 1, 120);
                if (after > before) { pp.Health = after; healed++; }
            }

            return healed > 0;
        }
    }

    public class PriestPrayerOfHealingActive : WarcraftCS2.Gameplay.IActiveSpell
    {
        public string Id => "priest.prayer_of_healing";
        public string Name => "Prayer of Healing";
        public string Description => "AoE-исцеление союзников вокруг кастера.";

        public bool OnCast(IWowRuntime rt, CCSPlayerController player)
        {
            if (rt is not WowmodCs2 plugin || player is not { IsValid: true }) return false;

            if (PrayerOfHealing.TryCast(plugin, player, out var reason))
                return true;

            rt.Print(player, $"[Warcraft] Prayer of Healing: {reason}.");
            return false;
        }
    }
}