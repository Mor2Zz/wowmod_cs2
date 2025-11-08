using System;
using CounterStrikeSharp.API.Core;

namespace wowmod_cs2
{
    // Реализация partial-хуков из WowDamage.Services.cs — чтобы урон/хил реально меняли HP
    public partial class WowmodCs2
    {
        partial void OnInstantDamageResolved(ulong casterSid, ulong targetSid, double finalDamage, double absorbed,
            WarcraftCS2.Spells.Systems.Damage.DamageSchool school, string? reason)
        {
            var vic = _wow_TryGetPlayer(targetSid);
            var pawn = vic?.PlayerPawn?.Value;
            if (pawn is null || !pawn.IsValid) return;

            int before = pawn.Health;
            int delta  = (int)Math.Round(finalDamage);
            int after  = Math.Max(1, before - delta);
            if (after < before) pawn.Health = after;
        }

        partial void OnPeriodicDamageResolved(ulong casterSid, ulong targetSid, double finalDamage, double absorbed,
            WarcraftCS2.Spells.Systems.Damage.DamageSchool school, string? reason)
        {
            var vic = _wow_TryGetPlayer(targetSid);
            var pawn = vic?.PlayerPawn?.Value;
            if (pawn is null || !pawn.IsValid) return;

            int before = pawn.Health;
            int delta  = (int)Math.Round(finalDamage);
            int after  = Math.Max(1, before - delta);
            if (after < before) pawn.Health = after;
        }

        partial void OnInstantHealResolved(ulong casterSid, ulong targetSid, double amount)
        {
            var tar  = _wow_TryGetPlayer(targetSid);
            var pawn = tar?.PlayerPawn?.Value;
            if (pawn is null || !pawn.IsValid) return;

            int before = pawn.Health;
            int delta  = (int)Math.Round(amount);
            int after  = Math.Min(120, before + delta);
            if (after > before) pawn.Health = after;
        }

        partial void OnPeriodicHealResolved(ulong casterSid, ulong targetSid, double amount)
        {
            var tar  = _wow_TryGetPlayer(targetSid);
            var pawn = tar?.PlayerPawn?.Value;
            if (pawn is null || !pawn.IsValid) return;

            int before = pawn.Health;
            int delta  = (int)Math.Round(amount);
            int after  = Math.Min(120, before + delta);
            if (after > before) pawn.Health = after;
        }
    }
}