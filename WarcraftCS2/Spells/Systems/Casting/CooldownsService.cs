namespace WarcraftCS2.Spells.Systems.Casting
{
    public sealed class CooldownsService
    {
        private readonly Dictionary<ulong, Dictionary<string, DateTime>> _map = new();

        private Dictionary<string, DateTime> GetMap(ulong sid)
        {
            if (!_map.TryGetValue(sid, out var d)) _map[sid] = d = new();
            return d;
        }

        public bool IsReady(ulong sid, string spellId, out double remainSec)
        {
            var d = GetMap(sid);
            if (d.TryGetValue(spellId, out var readyAt))
            {
                var now = DateTime.UtcNow;
                if (now < readyAt) { remainSec = (readyAt - now).TotalSeconds; return false; }
            }
            remainSec = 0;
            return true;
        }

        public void Start(ulong sid, string spellId, double sec)
            => GetMap(sid)[spellId] = DateTime.UtcNow.AddSeconds(sec);

        public void Clear(ulong sid, string spellId) => GetMap(sid).Remove(spellId);
        public void ResetAll(ulong sid) => _map.Remove(sid);
    }
}