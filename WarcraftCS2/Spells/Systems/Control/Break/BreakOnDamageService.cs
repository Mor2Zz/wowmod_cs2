using System;
using System.Collections.Generic;
using WarcraftCS2.Spells.Systems.Core.Runtime;

namespace WarcraftCS2.Spells.Systems.Control.Break
{
    /// Сервис «сломать контроль при уроне».
    /// - Подписывается на ProcBus.OnDamage;
    /// - Хранит активные CC с флагом BreakOnDamage;
    /// - По событию урона проверяет правило и вызывает breaker() (твой код снятия ауры/контроля).
    public sealed class BreakOnDamageService : IDisposable
    {
        // Singleton по умолчанию (можешь создать свой экземпляр, если хочешь)
        private static BreakOnDamageService? _instance;
        public static BreakOnDamageService Instance => _instance ??= new BreakOnDamageService();

        private readonly object _gate = new();
        // key: (victim, tag, spellId, caster) — чтобы поддерживать несколько одинаковых тегов от разных кастеров/спеллов
        private readonly Dictionary<(ulong tgt, string tag, int spell, ulong caster), (ActiveCc cc, Action breaker)> _map = new();

        private readonly IDisposable _subDamage;
        private IBreakOnDamageRule? _rule; // опциональный провайдер правил
        private ISpellRuntime? _rt;        // опционально — если нужен в правилах

        private BreakOnDamageService()
        {
            _subDamage = ProcBus.SubscribeDamage(OnDamage);
        }

        /// Можно поставить свой rule-провайдер (например, адаптер к твоему BreakOnDamageRules.cs).
        public void SetRuleProvider(IBreakOnDamageRule? rule, ISpellRuntime? rt = null)
        {
            lock (_gate)
            {
                _rule = rule;
                _rt = rt;
            }
        }

        /// Регистрирует брейкаемый контроль. breaker() — твой коллбек снятия (RemoveAuraByTag / RemoveControl).
        public void Register(in ActiveCc cc, Action breaker)
        {
            var key = (cc.TargetSid, cc.Tag, cc.SpellId, cc.CasterSid);
            lock (_gate)
            {
                _map[key] = (cc, breaker);
            }
        }

        /// Можно вручную снять регистрацию (например, когда контроль естественно закончился).
        public void Unregister(ulong targetSid, string tag, int spellId, ulong casterSid)
        {
            var key = (targetSid, tag, spellId, casterSid);
            lock (_gate) { _map.Remove(key); }
        }

        private void OnDamage(ProcBus.DamageArgs e)
        {
            // Быстрый путь: ничего не хранится — выходим
            if (_map.Count == 0) return;

            // Собираем все CC на этом таргете (обычно 0..1, но поддерживаем N)
            List<(ActiveCc cc, Action breaker, (ulong tgt, string tag, int spell, ulong caster) key)> toCheck = new();

            lock (_gate)
            {
                foreach (var kv in _map)
                {
                    if (kv.Key.tgt == e.TgtSid)
                        toCheck.Add((kv.Value.cc, kv.Value.breaker, kv.Key));
                }
            }

            if (toCheck.Count == 0) return;

            // Проверка правил и брейк
            foreach (var it in toCheck)
            {
                bool shouldBreak = false;

                var rule = _rule; var rt = _rt;

                if (rule != null && rt != null)
                {
                    shouldBreak = rule.ShouldBreak(in e, in it.cc, rt);
                }
                else
                {
                    // Дефолт: если задан флэт-порог — ломаем при уроне >= порога; иначе ломаем на любой ненулевой урон.
                    if (it.cc.BreakFlat > 0)
                        shouldBreak = e.Amount >= it.cc.BreakFlat;
                    else
                        shouldBreak = e.Amount > 0;
                }

                if (!shouldBreak) continue;

                try { it.breaker(); } catch { /* не рушим шину */ }

                lock (_gate) { _map.Remove(it.key); }
            }
        }

        public void Dispose()
        {
            _subDamage.Dispose();
            lock (_gate) { _map.Clear(); }
        }
    }
}