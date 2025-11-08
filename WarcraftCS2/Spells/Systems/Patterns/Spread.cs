using System;
using System.Collections.Generic;
using System.Numerics;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime; // ProcBus.PublishAuraApply
using WarcraftCS2.Spells.Systems.Core.LineOfSight;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    /// Массовое "распространение" заданных тегов с жертвы на соседей.
    /// Вариант без подписок/хуков: вызывается прямо в момент смерти/триггера.
    public static class Spread
    {
        public sealed class Config
        {
            public bool   RequireLoSBetweenSourceAndTarget = false;
            public bool   WorldOnly = false;

            /// Какие теги «распространяем». Пусто — ничего не делаем.
            public string[] TagsToSpread = Array.Empty<string>();

            /// Радиус поиска соседей.
            public float  Radius = 6f;
            /// Игнорировать высоту (плоская сфера)
            public bool   Flat = true;
            /// Максимум целей для применения.
            public int    MaxTargets = 5;

            /// Величина применяемого тега на цель.
            public float  ValuePerTag = 1f;
            /// Длительность накладываемого тега.
            public float  NewDuration = 4f;

            /// Только по врагам (true) или по всем (false).
            public bool   OnlyToEnemies = true;

            public int    SpellId = 0;
            public string? PlayFx;
            public string? PlaySfx;
        }

        /// Копирует (в смысле заново накладывает) указанные теги на ближайших кандидатов.
        /// Вызывать в момент смерти/взрыва эффекта у жертвы.
        public static void FromVictim(
            ISpellRuntime rt,
            TargetSnapshot caster,         // от чьего имени накладываем на соседей
            TargetSnapshot victim,         // центр радиуса (жертва/источник распространения)
            IReadOnlyList<TargetSnapshot> candidates,
            Config cfg)
        {
            if (cfg.TagsToSpread == null || cfg.TagsToSpread.Length == 0)
                return;

            int csid = rt.SidOf(caster);
            Vector3 center = victim.Position;
            float r2 = cfg.Radius * cfg.Radius;

            int appliedTargets = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                var t = candidates[i];
                if (!rt.IsAlive(t)) continue;
                if (cfg.OnlyToEnemies && !rt.IsEnemy(caster, t)) continue;

                // геометрия
                var p = t.Position;
                float dx = p.X - center.X, dy = p.Y - center.Y, dz = p.Z - center.Z;
                if (cfg.Flat) dz = 0f;
                float d2 = dx * dx + dy * dy + dz * dz;
                if (d2 > r2) continue;

                // LoS (опционально)
                if (cfg.RequireLoSBetweenSourceAndTarget)
                {
                    bool visible = cfg.WorldOnly
                        ? LineOfSight.VisibleWorldOnly(caster, t)
                        : LineOfSight.Visible(caster, t);
                    if (!visible) continue;
                }

                int tsid = rt.SidOf(t);
                float dur = MathF.Max(0.05f, cfg.NewDuration);
                float val = MathF.Max(0f, cfg.ValuePerTag);

                // накладываем каждый запрошенный тег независимо
                for (int j = 0; j < cfg.TagsToSpread.Length; j++)
                {
                    var tag = cfg.TagsToSpread[j];
                    if (string.IsNullOrWhiteSpace(tag)) continue;

                    rt.ApplyAura(csid, tsid, cfg.SpellId, tag, val, dur);
                    // уведомление в ProcBus (сигнал для других систем)
                    ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)csid, (ulong)tsid, tag, val, dur));
                }

                appliedTargets++;
                if (appliedTargets >= cfg.MaxTargets) break;
            }

            if (!string.IsNullOrEmpty(cfg.PlayFx))  rt.Fx(cfg.PlayFx!, victim);
            if (!string.IsNullOrEmpty(cfg.PlaySfx)) rt.Sfx(cfg.PlaySfx!, victim);
        }
    }
}