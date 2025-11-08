using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Runtime;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Наращивание щита "шардами" с ограничениями на один шард и общий потолок.
    public static class ShieldAccumulator
    {
        public sealed class Config
        {
            public int    SpellId;
            public string Tag = "acc-shield";

            public float  Duration = 6f;         // длительность апдейта щита (refresh на каждый шард)
            public float  MaxPerShard = 25f;      // потолок одного пополнения
            public float  MaxTotal    = 150f;     // общий потолок накопления в окне Duration

            public float  Mana = 0f;              // обычно 0, так как это реактив
            public float  Gcd  = 0f;
            public float  Cooldown = 0f;

            public string? PlayFxShard; public string? PlaySfxShard;
        }

        private struct Key : IEquatable<Key>
        {
            public int OwnerSid;
            public int SpellId;
            public string Tag;
            public bool Equals(Key other) => OwnerSid == other.OwnerSid && SpellId == other.SpellId && Tag == other.Tag;
            public override bool Equals(object? obj) => obj is Key k && Equals(k);
            public override int GetHashCode() => HashCode.Combine(OwnerSid, SpellId, Tag);
        }

        private sealed class Bucket
        {
            public float Total;
            public DateTime ExpireAt;
        }

        private static readonly Dictionary<Key, Bucket> _buckets = new();

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        // Пополнить щит владельцу (owner) на amount (шард), с лимитами.
        public static float AddShard(ISpellRuntime rt, TargetSnapshot owner, Config cfg, float amount)
        {
            if (!rt.IsAlive(owner)) return 0f;
            int osid = rt.SidOf(owner);

            float dur = MathF.Max(0.05f, cfg.Duration);
            float shard = Clamp(amount, 0f, cfg.MaxPerShard);

            var key = new Key { OwnerSid = osid, SpellId = cfg.SpellId, Tag = cfg.Tag };
            if (!_buckets.TryGetValue(key, out var bucket) || bucket.ExpireAt <= DateTime.UtcNow)
            {
                bucket = new Bucket { Total = 0f, ExpireAt = DateTime.UtcNow.AddSeconds(dur) };
                _buckets[key] = bucket;
            }

            float remaining = MathF.Max(0f, cfg.MaxTotal - bucket.Total);
            float add = MathF.Min(shard, remaining);
            if (add <= 0f) return 0f;

            // Применяем щит: ApplyShield агрегирует по (target, spellId, tag)
            rt.ApplyShield(osid, osid, cfg.SpellId, cfg.Tag, add, dur);
            ProcBus.PublishAuraApply(new ProcBus.AuraArgs(cfg.SpellId, (ulong)osid, (ulong)osid, cfg.Tag, add, dur));

            if (!string.IsNullOrEmpty(cfg.PlayFxShard))  rt.Fx(cfg.PlayFxShard!, owner);
            if (!string.IsNullOrEmpty(cfg.PlaySfxShard)) rt.Sfx(cfg.PlaySfxShard!, owner);

            bucket.Total += add;
            // продлеваем окно накопления, чтобы ряд быстрых шардов считать общим пулом
            bucket.ExpireAt = DateTime.UtcNow.AddSeconds(dur);
            return add;
        }

        // Служебная зачистка "протухших" корзин (можно вызывать из редких мест — по таймеру/ивенту)
        public static void CleanupExpired()
        {
            var now = DateTime.UtcNow;
            var toDel = new List<Key>();
            foreach (var kv in _buckets)
                if (kv.Value.ExpireAt <= now) toDel.Add(kv.Key);
            foreach (var k in toDel) _buckets.Remove(k);
        }
    }
}