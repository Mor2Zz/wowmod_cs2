using RPG.XP;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Spells.Systems.Core;
    /// <summary>
    /// Централизованная настройка кд/стоимости с учётом ролей и статусов.
    /// </summary>
    public static class SpellTuning
    {
        public static bool CanCast(ulong steamId) => !StatusStore.IsSilenced(steamId);

        /// <summary>
        /// Итоговый cooldown: роль → статусы (Haste/Slow) → кэп (0.25x..3x от базы)
        /// </summary>
        public static double AdjustCooldown(ulong steamId, PlayerRole role, double baseSeconds)
        {
            double cd = baseSeconds;

            // 1) роль (тонкая подстройка по умолчанию)
            cd *= role switch
            {
                PlayerRole.Support => 0.95, // -5%
                PlayerRole.Tank    => 1.05, // +5%
                _                  => 1.00
            };

            // 2) статусы
            cd *= StatusStore.CooldownMultiplier(steamId);

            // 3) кэп
            cd = System.Math.Clamp(cd, baseSeconds * 0.25, baseSeconds * 3.0);
            return cd;
        }

        /// <summary>
        /// Итоговая стоимость (если используешь ресурс): роль → (позже: статусы) → кэп
        /// </summary>
        public static int AdjustCost(ulong steamId, PlayerRole role, int baseCost)
        {
            double cost = baseCost * (role switch
            {
                PlayerRole.Support => 0.95,
                PlayerRole.Tank    => 1.05,
                _                  => 1.00
            });

            cost = System.Math.Clamp(cost, baseCost * 0.25, baseCost * 4.0);
            return (int)System.Math.Round(cost);
        }
    }
