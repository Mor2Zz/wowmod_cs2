using System;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using WarcraftCS2.Spells.Systems.Status;

namespace wowmod_cs2
{
    // Мини-патч: применяем теги скорости к реальному мультипликатору бега игрока.
    public partial class WowmodCs2
    {
        partial void OnMovementStateComputed(ulong sid, MovementState state)
        {
            var p = _wow_TryGetPlayer(sid);
            if (p is null || !p.IsValid) return;

            double mult = 1.0;

            // −35% на время Bladestorm (Whirlwind)
            if (Buffs.Has(sid, "warrior.whirlwind.bladestorm.selfslow35"))
                mult *= 0.65;

            // +20% на 2с при Warbringer/Warpath
            if (Buffs.Has(sid, "warrior.warbringer.warpath.haste20"))
                mult *= 1.20;

            // Жёсткие рамки, чтобы не сломать физику
            mult = Math.Clamp(mult, 0.40, 1.80);

            ApplyMoveSpeedMultiplier(p, mult);
        }

        private static void ApplyMoveSpeedMultiplier(CCSPlayerController player, double mult)
        {
            try
            {
                var pawn = player.PlayerPawn?.Value;
                if (pawn is null || !pawn.IsValid) return;

                float fm = (float)mult;

                // Попытка №1: VelocityModifier (часто доступно в CSS API)
                try
                {
                    var prop = pawn.GetType().GetProperty("VelocityModifier",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop is { CanWrite: true })
                    {
                        prop.SetValue(pawn, fm);
                        return;
                    }
                }
                catch { /* ignore */ }

                // Попытка №2: LaggedMovementValue (альтернативное имя)
                try
                {
                    var prop = pawn.GetType().GetProperty("LaggedMovementValue",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (prop is { CanWrite: true })
                    {
                        prop.SetValue(pawn, fm);
                        return;
                    }
                }
                catch { /* ignore */ }

                // Попытка №3: сетевое поле как поле
                try
                {
                    var field = pawn.GetType().GetField("m_flLaggedMovementValue",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (field is not null)
                    {
                        field.SetValue(pawn, fm);
                        return;
                    }
                }
                catch { /* ignore */ }
            }
            catch
            {
                // Ничего — если API поменялся, просто не трогаем скорость
            }
        }
    }
}