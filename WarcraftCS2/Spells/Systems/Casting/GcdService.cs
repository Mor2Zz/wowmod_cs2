namespace WarcraftCS2.Spells.Systems.Casting
{
    public sealed class GcdService
    {
        private readonly Dictionary<ulong, DateTime> _until = new();

        public bool IsOnGcd(ulong sid, out double remainSec)
        {
            if (_until.TryGetValue(sid, out var t))
            {
                var now = DateTime.UtcNow;
                if (now < t) { remainSec = (t - now).TotalSeconds; return true; }
            }
            remainSec = 0;
            return false;
        }

        public void Start(ulong sid, double sec) => _until[sid] = DateTime.UtcNow.AddSeconds(sec);
        public void Clear(ulong sid) => _until.Remove(sid);
    }
}