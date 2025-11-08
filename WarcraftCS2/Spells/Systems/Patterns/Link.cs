using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems;
using WarcraftCS2.Spells.Systems.Core.Targeting;
using WarcraftCS2.Spells.Systems.Core.Runtime; // ProcBus

namespace WarcraftCS2.Spells.Systems.Patterns
{
    // Связанные ауры: деление получаемого урона между участниками группы.
    // Реализация: при входящем уроне на одного — лечим его часть и наносим остальным недостающую долю.
    public static class Link
    {
        private sealed class Group
        {
            public string Tag = "link";
            public int SpellId;
            public float ShareEach01; // равная доля для каждого участника (в т.ч. пострадавшего)
            public List<ulong> Members = new();
            public DateTime ExpireAt;
        }

        private static readonly Dictionary<string, Group> _groupsById = new(); // groupId -> Group
        private static readonly Dictionary<ulong, string> _groupIdByMember = new(); // member -> groupId
        private static bool _hooked;
        private static bool _inDispatch;
        private static WeakReference<ISpellRuntime>? _rtRef;

        private static void EnsureHooked()
        {
            if (_hooked) return;
            _hooked = true;

            ProcBus.SubscribeDamage(args =>
            {
                if (_inDispatch) return;
                // ожидаем поля: SpellId, SrcSid, TgtSid, Amount, School
                var tgt = args.TgtSid;
                if (!_groupIdByMember.TryGetValue(tgt, out var gid)) return;
                if (!_groupsById.TryGetValue(gid, out var g)) return;
                if (DateTime.UtcNow > g.ExpireAt) { RemoveGroup(gid); return; }

                if (_rtRef == null || !_rtRef.TryGetTarget(out var rt)) return;

                int n = g.Members.Count;
                if (n <= 1) return;

                float desiredEach = args.Amount * (1f / n); // простое равное деление
                float healBack = args.Amount - desiredEach; // сколько вернуть пострадавшему
                if (healBack > 0f)
                {
                    _inDispatch = true;
                    try
                    {
                        rt.Heal((int)args.SrcSid, (int)tgt, g.SpellId, healBack);
                        for (int i = 0; i < g.Members.Count; i++)
                        {
                            var m = g.Members[i];
                            if (m == tgt) continue;
                            rt.DealDamage((int)args.SrcSid, (int)m, g.SpellId, desiredEach, args.School);
                        }
                    }
                    finally { _inDispatch = false; }
                }
            });
        }

        private static string MakeGroupId(int ownerSid, int spellId, string tag)
            => $"link:{ownerSid}:{spellId}:{tag}";

        private static void RemoveGroup(string groupId)
        {
            if (!_groupsById.TryGetValue(groupId, out var g)) return;
            _groupsById.Remove(groupId);
            for (int i = 0; i < g.Members.Count; i++)
                _groupIdByMember.Remove(g.Members[i]);
        }

        public sealed class CreateConfig
        {
            public int    SpellId;
            public string Tag = "link";
            public float  Duration = 6f;
            public float  ShareEach01 = -1f; // <=0 => равномерно по количеству участников
            public string AuraTag = "link";  // тег, который повесим на участников (для визуализации/логики)
        }

        // Создать группу линка из списка участников. Все доли равные.
        public static SpellResult Create(
            ISpellRuntime rt,
            TargetSnapshot owner,
            IReadOnlyList<TargetSnapshot> members,
            CreateConfig cfg)
        {
            if (members == null || members.Count == 0) return SpellResult.Fail();

            _rtRef = new WeakReference<ISpellRuntime>(rt);
            EnsureHooked();

            int osid = rt.SidOf(owner);
            string gid = MakeGroupId(osid, cfg.SpellId, cfg.Tag);

            // Старая группа с тем же id — удалить
            RemoveGroup(gid);

            var g = new Group
            {
                Tag = cfg.Tag,
                SpellId = cfg.SpellId,
                ShareEach01 = cfg.ShareEach01,
                ExpireAt = DateTime.UtcNow.AddSeconds(MathF.Max(0.05f, cfg.Duration)),
            };

            for (int i = 0; i < members.Count; i++)
            {
                var t = members[i];
                if (!rt.IsAlive(t)) continue;
                int tsid = rt.SidOf(t);
                var uid = (ulong)tsid;

                g.Members.Add(uid);
                _groupIdByMember[uid] = gid;

                // навесим короткую ауру (продлевается ниже таймером)
                rt.ApplyAura(osid, tsid, cfg.SpellId, cfg.AuraTag, 1f, 0.3f);
            }

            if (g.Members.Count <= 1)
            {
                RemoveGroup(gid);
                return SpellResult.Fail();
            }

            if (g.ShareEach01 <= 0f) g.ShareEach01 = 1f / g.Members.Count;
            _groupsById[gid] = g;

            // Простая поддержка ауры и авто-удаление по окончании
            rt.StartPeriodic(
                osid, osid, cfg.SpellId,
                MathF.Max(0.05f, cfg.Duration),
                0.25f,
                onTick: () =>
                {
                    if (DateTime.UtcNow > g.ExpireAt) return;
                    for (int i = 0; i < g.Members.Count; i++)
                    {
                        var uid = g.Members[i];
                        rt.ApplyAura(osid, (int)uid, cfg.SpellId, cfg.AuraTag, 1f, 0.5f);
                    }
                },
                onEnd: () =>
                {
                    for (int i = 0; i < g.Members.Count; i++)
                    {
                        var uid = g.Members[i];
                        rt.RemoveAuraByTag((int)uid, cfg.AuraTag);
                    }
                    RemoveGroup(gid);
                });

            return SpellResult.Ok(0f, 0f);
        }
    }
}
