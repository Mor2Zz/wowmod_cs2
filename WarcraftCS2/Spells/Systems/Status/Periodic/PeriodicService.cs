using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems.Damage;

namespace WarcraftCS2.Spells.Systems.Status.Periodic
{
    /// Хранилище и тики DoT/HoT. Ничего «не знает» про CS2 — взаимодействие через колбэки с SteamID.
    public sealed class PeriodicService
    {
        // targetSid -> список периодических эффектов
        private readonly Dictionary<ulong, List<PeriodicEntry>> _store = new();

        public void AddOrRefreshDot(
            ulong casterSid, ulong targetSid, string id,
            DamageSchool school,
            double amountPerTick, double intervalSec, double durationSec,
            int addStacks = 0, int maxStacks = 1)
        {
            Upsert(casterSid, targetSid, id, PeriodicKind.DoT, school, amountPerTick, intervalSec, durationSec, addStacks, maxStacks);
        }

        public void AddOrRefreshHot(
            ulong casterSid, ulong targetSid, string id,
            double amountPerTick, double intervalSec, double durationSec,
            int addStacks = 0, int maxStacks = 1)
        {
            Upsert(casterSid, targetSid, id, PeriodicKind.HoT, DamageSchool.Arcane, amountPerTick, intervalSec, durationSec, addStacks, maxStacks);
        }

        public bool Remove(ulong targetSid, string id)
        {
            if (!_store.TryGetValue(targetSid, out var list)) return false;
            var idx = list.FindIndex(e => e.Id == id);
            if (idx < 0) return false;
            list.RemoveAt(idx);
            return true;
        }

        public void Sweep(DateTime nowUtc)
        {
            foreach (var kv in _store)
            {
                var lst = kv.Value;
                for (int i = lst.Count - 1; i >= 0; i--)
                {
                    if (lst[i].UntilUtc <= nowUtc) lst.RemoveAt(i);
                }
            }
        }

        /// Тикает периодику. Для каждого тика вызывает applyDamage/applyHeal.
        /// Колбэки принимают (casterSid, targetSid, amount, [school]) и возвращают true, если применили.
        public void Tick(
            DateTime nowUtc,
            Func<ulong, bool> isValidSid,
            Func<ulong, ulong, double, DamageSchool, bool> applyDamage,
            Func<ulong, ulong, double, bool> applyHeal)
        {
            foreach (var kv in _store)
            {
                var targetSid = kv.Key;
                var list = kv.Value;
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];

                    if (e.UntilUtc <= nowUtc) continue;
                    if (e.NextTickUtc > nowUtc) continue;

                    // валидация сидов
                    if (!isValidSid(targetSid) || !isValidSid(e.CasterSid))
                    {
                        // если цель/кастер ушли — просто пропускаем этот тик
                        e.NextTickUtc = nowUtc.AddSeconds(e.IntervalSec);
                        list[i] = e;
                        continue;
                    }

                    double total = e.AmountPerTick * Math.Max(1, e.Stacks);
                    bool applied = false;

                    if (e.Kind == PeriodicKind.DoT)
                        applied = applyDamage(e.CasterSid, targetSid, total, e.School);
                    else
                        applied = applyHeal(e.CasterSid, targetSid, total);

                    // даже если не применили — сдвигаем следующий тик, чтобы не «забомбить»
                    e.NextTickUtc = nowUtc.AddSeconds(e.IntervalSec);
                    list[i] = e;
                }
            }
        }

        // ------------- internal -------------
        private void Upsert(
            ulong casterSid, ulong targetSid, string id, PeriodicKind kind, DamageSchool school,
            double amountPerTick, double intervalSec, double durationSec, int addStacks, int maxStacks)
        {
            if (string.IsNullOrWhiteSpace(id) || intervalSec <= 0 || durationSec <= 0 || amountPerTick <= 0) return;

            var list = GetList(targetSid);
            var now = DateTime.UtcNow;
            var until = now.AddSeconds(durationSec);

            var idx = list.FindIndex(e => e.Id == id);
            if (idx < 0)
            {
                list.Add(new PeriodicEntry
                {
                    Id = id,
                    Kind = kind,
                    CasterSid = casterSid,
                    TargetSid = targetSid,
                    School = school,
                    AmountPerTick = amountPerTick,
                    IntervalSec = intervalSec,
                    NextTickUtc = now.AddSeconds(intervalSec),
                    UntilUtc = until,
                    Stacks = Math.Max(1, Math.Min(Math.Max(1, maxStacks), Math.Max(1, addStacks == 0 ? 1 : addStacks))),
                    MaxStacks = Math.Max(1, maxStacks)
                });
                return;
            }

            var e = list[idx];
            e.UntilUtc = until;
            e.AmountPerTick = amountPerTick; // разрешаем обновлять силу тика
            e.IntervalSec = intervalSec;
            e.School = school;
            e.CasterSid = casterSid;
            if (addStacks != 0)
                e.Stacks = Math.Min(e.MaxStacks, e.Stacks + Math.Max(0, addStacks));
            e.MaxStacks = Math.Max(e.MaxStacks, maxStacks);
            list[idx] = e;
        }

        private List<PeriodicEntry> GetList(ulong sid)
        {
            if (!_store.TryGetValue(sid, out var list))
            {
                list = new List<PeriodicEntry>(4);
                _store[sid] = list;
            }
            return list;
        }
    }
}