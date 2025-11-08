namespace WarcraftCS2.Spells.Systems.Resources
{
    public sealed class ResourceService
    {
        private readonly Dictionary<ulong, ResourceState> _state = new();
        private readonly Config.CombatConfig _cfg;

        public ResourceService(Config.CombatConfig cfg) => _cfg = cfg;

        public ResourceState Get(ulong sid)
        {
            if (!_state.TryGetValue(sid, out var rs))
            {
                rs = new ResourceState
                {
                    MaxMana = _cfg.BaseMaxMana,
                    Mana = _cfg.BaseMaxMana,
                    RegenPerSec = _cfg.BaseManaRegenPerSec
                };
                _state[sid] = rs;
            }
            return rs;
        }

        public void SetMaxMana(ulong sid, double maxMana)
        {
            var r = Get(sid);
            r.MaxMana = maxMana;
            r.Mana = Math.Min(r.Mana, r.MaxMana);
        }

        public bool TryConsume(ulong sid, double amount)
        {
            var r = Get(sid);
            if (r.Mana + 1e-6 < amount) return false;
            r.Mana -= amount;
            r.LastCastAt = DateTime.UtcNow;
            return true;
        }

        public void MarkCast(ulong sid)
        {
            var r = Get(sid);
            r.LastCastAt = DateTime.UtcNow;
        }

        // 1 Гц реген — дергается таймером из CombatServices
        public void TickRegen()
        {
            foreach (var rs in _state.Values)
            {
                rs.Mana = Math.Min(rs.MaxMana, rs.Mana + rs.RegenPerSec);
            }
        }

        public void Reset(ulong sid) => _state.Remove(sid);
    }

    public sealed class ResourceState
    {
        public double Mana { get; set; }
        public double MaxMana { get; set; }
        public double RegenPerSec { get; set; }
        public DateTime LastCastAt { get; set; }
    }
}