using System;
using WarcraftCS2.Spells.Systems.Damage.Services;
using WarcraftCS2.Spells.Systems.Status;

namespace WarcraftCS2.Spells.Systems.Damage
{
    /// Единая точка расчёта входящего спеллового урона.
    /// Порядок:
    ///   1) проверка иммунитета по школе (иммун → false);
    ///   2) применение статусных множителей/кэпа (Status.DamageModifiers);
    ///   3) поглощение щитами (ShieldService) — приоритеты внутри сервиса;
    ///   4) возврат итогового урона, абсорба и причины отказа (только для иммунитета).
    public sealed class DamagePipeline
    {
        private readonly ImmunityService _immune;
        private readonly ShieldService _shields;

        public DamagePipeline(ImmunityService immune, ShieldService shields)
        {
            _immune  = immune;
            _shields = shields;
        }

        /// Расчёт финального урона спелла с учётом иммунитета, статусных множителей/кэпа и щитов.
        /// Возвращает true — если есть, что применять (урон или абсорб); false — если полностью заблокировано иммунитетом.
        
        /// <param name="attackerSid">SteamID атакующего</param>
        /// <param name="victimSid">SteamID жертвы</param>
        /// <param name="baseDamage">исходный урон (после резистов)</param>
        /// <param name="school">школа урона</param>
        /// <param name="finalDamage">итоговый урон (после абсорба)</param>
        /// <param name="absorbed">сколько поглотили щиты</param>
        /// <param name="failReason">если иммунитет — сюда текст причины</param>
        public bool ResolveIncoming(
            ulong attackerSid,
            ulong victimSid,
            double baseDamage,
            DamageSchool school,
            out double finalDamage,
            out double absorbed,
            out string? failReason)
        {
        // Demoralizing Shout: reduce outgoing damage from attacker
        if (WarcraftCS2.Spells.Systems.Status.Buffs.Has(attackerSid, "warrior.demoralizing_shout") ||
            WarcraftCS2.Spells.Systems.Status.Buffs.Has(attackerSid, "warrior.demoralizing_shout.20"))
        {
            baseDamage *= 0.80; // -20%
        }

            finalDamage = 0;
            absorbed    = 0;
            failReason  = null;

            // 1) Жёсткий иммунитет по школе
            if (_immune.IsImmune(victimSid, school))
            {
                failReason = "Иммунитет";
                return false; // полностью отменяем событие урона
            }

            // 2) Статусные модификаторы/кап (StatusStore). Маппим school -> kind.
            var kind = MapSchoolToKind(school);
            var afterMods = DamageModifiers.Apply(baseDamage, victimSid, kind);
            if (afterMods <= 0)
            {
                // Урон полностью ушёл в ноль статусами (редукции/кэпы) — это НЕ иммунитет.
                // Семантика прежняя: раз ни урона, ни абсорба — вернём false, чтобы не спамить onResolved.
                return false;
            }

            // 3) Щиты
            var afterShields = _shields.Apply(victimSid, afterMods, out absorbed);
            finalDamage = Math.Max(0, afterShields);

            // Применять есть смысл, если что-то пробило или что-то поглотилось
            return finalDamage > 0.0 || absorbed > 0.0;
        }

        private static Status.DamageKind MapSchoolToKind(DamageSchool s)
            => s == DamageSchool.Physical ? Status.DamageKind.Physical : Status.DamageKind.Magic;
    }
}