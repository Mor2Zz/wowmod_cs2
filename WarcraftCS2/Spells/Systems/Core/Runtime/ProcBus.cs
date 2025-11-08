using System;
using System.Threading;
using WarcraftCS2.Spells.Systems.Core.Targeting;

namespace WarcraftCS2.Spells.Systems.Core.Runtime
{
    /// Лёгкая шина событий спеллов (on-hit/on-heal/periodic/channel/aura/control).
    public static class ProcBus
    {
        // --- аргументы событий ---

        public readonly struct DamageArgs
        {
            public readonly int SpellId;
            public readonly ulong SrcSid;
            public readonly ulong TgtSid;
            public readonly float Amount;
            public readonly string School;
            public readonly bool Critical;
            public DamageArgs(int spellId, ulong srcSid, ulong tgtSid, float amount, string school, bool critical = false)
            { SpellId = spellId; SrcSid = srcSid; TgtSid = tgtSid; Amount = amount; School = school; Critical = critical; }
        }

        public readonly struct HealArgs
        {
            public readonly int SpellId;
            public readonly ulong SrcSid;
            public readonly ulong TgtSid;
            public readonly float Amount;
            public HealArgs(int spellId, ulong srcSid, ulong tgtSid, float amount)
            { SpellId = spellId; SrcSid = srcSid; TgtSid = tgtSid; Amount = amount; }
        }

        public readonly struct PeriodicTickArgs
        {
            public readonly int SpellId;
            public readonly ulong SrcSid;
            public readonly ulong TgtSid;
            public readonly float TickAmount;
            public readonly string Kind; // "dot" / "hot"
            public PeriodicTickArgs(int spellId, ulong srcSid, ulong tgtSid, float tickAmount, string kind)
            { SpellId = spellId; SrcSid = srcSid; TgtSid = tgtSid; TickAmount = tickAmount; Kind = kind; }
        }

        public readonly struct ChannelTickArgs
        {
            public readonly int SpellId;
            public readonly ulong SrcSid;
            public readonly ulong TgtSid;
            public readonly float TickAmount;
            public readonly string School;
            public ChannelTickArgs(int spellId, ulong srcSid, ulong tgtSid, float tickAmount, string school)
            { SpellId = spellId; SrcSid = srcSid; TgtSid = tgtSid; TickAmount = tickAmount; School = school; }
        }

        public readonly struct AuraArgs
        {
            public readonly int SpellId;
            public readonly ulong SrcSid;
            public readonly ulong TgtSid;
            public readonly string Tag;     // "shield", "immune:magic", "dot", ...
            public readonly float Magnitude;
            public readonly float Duration;
            public AuraArgs(int spellId, ulong srcSid, ulong tgtSid, string tag, float magnitude, float duration)
            { SpellId = spellId; SrcSid = srcSid; TgtSid = tgtSid; Tag = tag; Magnitude = magnitude; Duration = duration; }
        }

        public readonly struct ControlArgs
        {
            public readonly int SpellId;
            public readonly ulong SrcSid;
            public readonly ulong TgtSid;
            public readonly string Tag;   // "stun","root","silence","fear","poly","disarm"
            public readonly float Duration;
            public ControlArgs(int spellId, ulong srcSid, ulong tgtSid, string tag, float duration)
            { SpellId = spellId; SrcSid = srcSid; TgtSid = tgtSid; Tag = tag; Duration = duration; }
        }

        // --- подписки ---

        private static readonly object _gate = new();

        private static event Action<DamageArgs>? _onDamage;
        private static event Action<HealArgs>? _onHeal;
        private static event Action<PeriodicTickArgs>? _onPeriodicTick;
        private static event Action<ChannelTickArgs>? _onChannelTick;
        private static event Action<AuraArgs>? _onAuraApply;
        private static event Action<AuraArgs>? _onAuraRemove;
        private static event Action<ControlArgs>? _onControlApply;

        private sealed class Unsub : IDisposable
        {
            private Action? _dispose;
            public Unsub(Action dispose) { _dispose = dispose; }
            public void Dispose() { Interlocked.Exchange(ref _dispose, null)?.Invoke(); }
        }

        public static IDisposable SubscribeDamage(Action<DamageArgs> cb)
        { lock (_gate) { _onDamage += cb; return new Unsub(() => { lock (_gate) _onDamage -= cb; }); } }

        public static IDisposable SubscribeHeal(Action<HealArgs> cb)
        { lock (_gate) { _onHeal += cb; return new Unsub(() => { lock (_gate) _onHeal -= cb; }); } }

        public static IDisposable SubscribePeriodicTick(Action<PeriodicTickArgs> cb)
        { lock (_gate) { _onPeriodicTick += cb; return new Unsub(() => { lock (_gate) _onPeriodicTick -= cb; }); } }

        public static IDisposable SubscribeChannelTick(Action<ChannelTickArgs> cb)
        { lock (_gate) { _onChannelTick += cb; return new Unsub(() => { lock (_gate) _onChannelTick -= cb; }); } }

        public static IDisposable SubscribeAuraApply(Action<AuraArgs> cb)
        { lock (_gate) { _onAuraApply += cb; return new Unsub(() => { lock (_gate) _onAuraApply -= cb; }); } }

        public static IDisposable SubscribeAuraRemove(Action<AuraArgs> cb)
        { lock (_gate) { _onAuraRemove += cb; return new Unsub(() => { lock (_gate) _onAuraRemove -= cb; }); } }

        public static IDisposable SubscribeControlApply(Action<ControlArgs> cb)
        { lock (_gate) { _onControlApply += cb; return new Unsub(() => { lock (_gate) _onControlApply -= cb; }); } }

        // --- публикация (вызов из твоих сервисов/рантайма) ---

        public static void PublishDamage(DamageArgs e) { var h = _onDamage; h?.Invoke(e); }
        public static void PublishHeal(HealArgs e) { var h = _onHeal; h?.Invoke(e); }
        public static void PublishPeriodicTick(PeriodicTickArgs e) { var h = _onPeriodicTick; h?.Invoke(e); }
        public static void PublishChannelTick(ChannelTickArgs e) { var h = _onChannelTick; h?.Invoke(e); }
        public static void PublishAuraApply(AuraArgs e) { var h = _onAuraApply; h?.Invoke(e); }
        public static void PublishAuraRemove(AuraArgs e) { var h = _onAuraRemove; h?.Invoke(e); }
        public static void PublishControlApply(ControlArgs e) { var h = _onControlApply; h?.Invoke(e); }

                // --- служебные утилиты шины ---

        public static void ResetAllSubscribers()
        {
            lock (_gate)
            {
                _onDamage = null;
                _onHeal = null;
                _onPeriodicTick = null;
                _onChannelTick = null;
                _onAuraApply = null;
                _onAuraRemove = null;
                _onControlApply = null;
            }
        }

        public static (int damage, int heal, int periodic, int channel, int auraApply, int auraRemove, int controlApply) SubscriberCounts()
        {
            lock (_gate)
            {
                int cnt(Action<DamageArgs>? d)           => d?.GetInvocationList().Length ?? 0;
                int cnth(Action<HealArgs>? d)            => d?.GetInvocationList().Length ?? 0;
                int cntp(Action<PeriodicTickArgs>? d)    => d?.GetInvocationList().Length ?? 0;
                int cntc(Action<ChannelTickArgs>? d)     => d?.GetInvocationList().Length ?? 0;
                int cnta(Action<AuraArgs>? d)            => d?.GetInvocationList().Length ?? 0;
                int cntctrl(Action<ControlArgs>? d)      => d?.GetInvocationList().Length ?? 0;

                return (
                    cnt(_onDamage),
                    cnth(_onHeal),
                    cntp(_onPeriodicTick),
                    cntc(_onChannelTick),
                    cnta(_onAuraApply),
                    cnta(_onAuraRemove),
                    cntctrl(_onControlApply)
                );
            }
        }
    }
}