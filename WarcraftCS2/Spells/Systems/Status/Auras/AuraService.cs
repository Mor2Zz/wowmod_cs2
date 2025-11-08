using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems.Damage.Services; 

namespace WarcraftCS2.Spells.Systems.Status
{
    /// Лёгкое хранилище аур: добавление/рефреш/стаки/диспел/свип.
    /// Не применяет эффектов — только хранение и правила стакинга.
    public sealed class AuraService
    {
        private readonly Dictionary<ulong, List<AuraState>> _store = new();
        private readonly DispelService _dispel;

        public AuraService(DispelService dispel) => _dispel = dispel;

        /// Добавить/рефрешнуть ауру на цели.
        public void AddOrRefresh(
            ulong targetSid,
            string auraId,
            AuraCategory categories,
            double durationSec,
            ulong sourceSid,
            int addStacks = 1,
            int maxStacks = 1,
            AuraRefreshMode mode = AuraRefreshMode.RefreshDuration_AddStackCap,
            double magnitude = 0.0)
        {
            if (string.IsNullOrWhiteSpace(auraId) || durationSec <= 0) return;

            var list = GetList(targetSid);
            var now = DateTime.UtcNow;
            var until = now.AddSeconds(durationSec);

            var idx = list.FindIndex(a => a.AuraId == auraId);
            if (idx < 0)
            {
                list.Add(new AuraState
                {
                    AuraId = auraId,
                    Categories = categories,
                    SourceSid = sourceSid,
                    Stacks = Math.Max(1, Math.Min(addStacks, Math.Max(1, maxStacks))),
                    MaxStacks = Math.Max(1, maxStacks),
                    Until = until,
                    Magnitude = magnitude
                });
                return;
            }

            var st = list[idx];

            switch (mode)
            {
                case AuraRefreshMode.RefreshDuration_AddStackCap:
                    st.Until = until;
                    st.Stacks = Math.Min(st.MaxStacks, st.Stacks + Math.Max(0, addStacks));
                    break;

                case AuraRefreshMode.RefreshDuration_KeepStacks:
                    st.Until = until;
                    break;

                case AuraRefreshMode.AddStack_KeepDuration:
                    st.Stacks = Math.Min(st.MaxStacks, st.Stacks + Math.Max(0, addStacks));
                    break;
            }

            st.Categories = categories;
            st.SourceSid = sourceSid;
            st.MaxStacks = Math.Max(st.MaxStacks, maxStacks);

            // если передана ненулевая новая величина — обновляем
            if (magnitude > 0) st.Magnitude = magnitude;

            list[idx] = st;
        }

        public bool Remove(ulong targetSid, string auraId)
        {
            if (!_store.TryGetValue(targetSid, out var list)) return false;
            var idx = list.FindIndex(a => a.AuraId == auraId);
            if (idx < 0) return false;
            list.RemoveAt(idx);
            return true;
        }

        public bool Has(ulong targetSid, string auraId)
        {
            if (!_store.TryGetValue(targetSid, out var list)) return false;
            var now = DateTime.UtcNow;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].Until <= now) { list.RemoveAt(i); continue; }
                if (list[i].AuraId == auraId) return true;
            }
            return false;
        }

        public int GetStacks(ulong targetSid, string auraId)
        {
            if (!_store.TryGetValue(targetSid, out var list)) return 0;
            var now = DateTime.UtcNow;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var a = list[i];
                if (a.Until <= now) { list.RemoveAt(i); continue; }
                if (a.AuraId == auraId) return a.Stacks;
            }
            return 0;
        }

        public double GetRemainingSec(ulong targetSid, string auraId)
        {
            if (!_store.TryGetValue(targetSid, out var list)) return 0;
            var now = DateTime.UtcNow;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var a = list[i];
                if (a.Until <= now) { list.RemoveAt(i); continue; }
                if (a.AuraId == auraId) return Math.Max(0, (a.Until - now).TotalSeconds);
            }
            return 0;
        }

        /// Диспел аур по маске категорий (совместно с DispelService).
        /// Возвращает сколько реально сняли.
        public int Dispel(ulong targetSid, AuraCategory allowMask, int maxCount = 1)
        {
            int removedByMask = 0;
            if (!_store.TryGetValue(targetSid, out var list) || list.Count == 0)
            {
                return _dispel.Dispel(targetSid, allowMask, maxCount);
            }

            var now = DateTime.UtcNow;
            for (int i = list.Count - 1; i >= 0 && removedByMask < maxCount; i--)
            {
                var a = list[i];
                if (a.Until <= now) { list.RemoveAt(i); continue; }
                if ((a.Categories & allowMask) != 0)
                {
                    list.RemoveAt(i);
                    removedByMask++;
                }
            }

            if (removedByMask < maxCount)
                removedByMask += _dispel.Dispel(targetSid, allowMask, maxCount - removedByMask);

            return removedByMask;
        }

        public void Sweep(DateTime nowUtc)
        {
            foreach (var kv in _store)
            {
                var lst = kv.Value;
                for (int i = lst.Count - 1; i >= 0; i--)
                    if (lst[i].Until <= nowUtc) lst.RemoveAt(i);
            }
        }

        public IReadOnlyList<AuraState> GetAll(ulong targetSid)
        {
            if (!_store.TryGetValue(targetSid, out var list)) return Array.Empty<AuraState>();
            return list;
        }

        private List<AuraState> GetList(ulong sid)
        {
            if (!_store.TryGetValue(sid, out var list))
            {
                list = new List<AuraState>(capacity: 4);
                _store[sid] = list;
            }
            return list;
        }
    }
}