using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace WarcraftCS2.Spells.Systems.Status;

    public sealed class StatusEffect
    {
        public string Id { get; init; } = "";
        public StatusTag Tags { get; init; } = StatusTag.None;
        public DamageKind? Kind { get; init; } // null = любой тип
        public double Multiplier { get; init; } = 1.0; // Reduce(<1) / Bonus(>1) / Haste/Slow
        public double? CapValue { get; init; } // для CapDamage: максимум урона после множителей
        public double ExpiresAt { get; init; } // unixtime sec
        public int Stacks { get; init; } = 1;
        public string? SourceSpell { get; init; }
    }

    /// <summary>
    /// Глобальное хранилище статусов. Ленивая очистка на каждом обращении.
    /// </summary>
    public static class StatusStore
    {
        private static readonly ConcurrentDictionary<ulong, List<StatusEffect>> _byPlayer = new();

        private static double Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

        public static void ClearPlayer(ulong steamId) => _byPlayer.TryRemove(steamId, out _);
        public static void ClearAll() => _byPlayer.Clear();

        public static void Add(
            ulong steamId,
            string id,
            StatusTag tags,
            double durationSec,
            DamageKind? kind = null,
            double multiplier = 1.0,
            double? capValue = null,
            int stacks = 1,
            string? sourceSpell = null)
        {
            var list = _byPlayer.GetOrAdd(steamId, _ => new List<StatusEffect>());
            var now = Now();
            var eff = new StatusEffect
            {
                Id = id,
                Tags = tags,
                Kind = kind,
                Multiplier = multiplier,
                CapValue = capValue,
                ExpiresAt = now + Math.Max(0.01, durationSec),
                Stacks = Math.Max(1, stacks),
                SourceSpell = sourceSpell
            };
            lock (list)
            {
                list.Add(eff);
                list.RemoveAll(e => e.ExpiresAt < now);
            }
        }

        public static bool Has(ulong steamId, StatusTag tag, DamageKind? kind = null)
        {
            if (!_byPlayer.TryGetValue(steamId, out var list)) return false;
            var now = Now();
            lock (list)
            {
                list.RemoveAll(e => e.ExpiresAt < now);
                return list.Any(e =>
                    (e.Tags & tag) != 0 &&
                    (e.Kind is null || kind is null || e.Kind == kind));
            }
        }

        /// <summary>
        /// Итоговый множитель входящего урона для жертвы против данного типа.
        /// Порядок: иммунитеты → редукции → бонусы → кап.
        /// Возврат: (finalMultiplier, capAfterMultipliers?).
        /// </summary>
        public static (double multiplier, double? capAfter) IncomingDamageFor(ulong victimId, DamageKind kind)
        {
            if (!_byPlayer.TryGetValue(victimId, out var list)) return (1.0, null);

            var now = Now();
            double mul = 1.0;
            double? cap = null;

            lock (list)
            {
                list.RemoveAll(e => e.ExpiresAt < now);

                // 1) Иммунитеты
                if (list.Any(e =>
                        (e.Tags & StatusTag.ImmuneAll) != 0 ||
                        ((e.Tags & StatusTag.ImmunePhysical) != 0 && kind == DamageKind.Physical) ||
                        ((e.Tags & StatusTag.ImmuneMagic) != 0 && kind == DamageKind.Magic)))
                {
                    return (0.0, 0.0);
                }

                // 2) Редукции
                foreach (var e in list)
                {
                    if ((e.Tags & StatusTag.ReduceDamage) == 0) continue;
                    if (e.Kind is not null && e.Kind != kind) continue;
                    mul *= Math.Clamp(e.Multiplier, 0.0, 1.0);
                }

                // 3) Бонусы
                foreach (var e in list)
                {
                    if ((e.Tags & StatusTag.BonusDamage) == 0) continue;
                    if (e.Kind is not null && e.Kind != kind) continue;
                    mul *= Math.Max(1.0, e.Multiplier);
                }

                // 4) Кэп
                foreach (var e in list)
                {
                    if ((e.Tags & StatusTag.CapDamage) == 0) continue;
                    if (e.Kind is not null && e.Kind != kind) continue;
                    if (e.CapValue is null) continue;

                    cap = cap is null ? e.CapValue : Math.Min(cap.Value, e.CapValue.Value);
                }
            }

            return (mul, cap);
        }

        // ---- Утилиты под кд/стоимость/сайлент ----

        public static double CooldownMultiplier(ulong steamId)
        {
            if (!_byPlayer.TryGetValue(steamId, out var list)) return 1.0;
            var now = Now();
            double mul = 1.0;
            lock (list)
            {
                list.RemoveAll(e => e.ExpiresAt < now);
                foreach (var e in list)
                {
                    if ((e.Tags & StatusTag.Haste) != 0) mul *= Math.Clamp(e.Multiplier, 0.1, 1.0);
                    if ((e.Tags & StatusTag.Slow)  != 0) mul *= Math.Max(1.0, e.Multiplier);
                }
            }
            return mul;
        }

        public static bool IsSilenced(ulong steamId)
        {
            if (!_byPlayer.TryGetValue(steamId, out var list)) return false;
            var now = Now();
            lock (list)
            {
                list.RemoveAll(e => e.ExpiresAt < now);
                return list.Any(e => (e.Tags & StatusTag.Silence) != 0);
            }
        }
    }