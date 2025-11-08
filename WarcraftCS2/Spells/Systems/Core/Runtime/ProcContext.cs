namespace WarcraftCS2.Spells.Systems.Core.Runtime
{
    // Утилита для удобных прóков "на игрока": общий сид, корневой ключ и обёртки над ProcRandom.
    public sealed class ProcContext
    {
        private readonly ProcRandom _rng;
        private readonly ProcTimer _timer;
        private readonly ulong _sid;
        private readonly string _root;

        public ProcContext(ProcRandom rng, ProcTimer timer, ulong steamId, string rootKey)
        {
            _rng = rng;
            _timer = timer;
            _sid = steamId;
            _root = rootKey ?? "";
        }

        public double NowSeconds => _timer.NowSeconds;

        public string Key(string suffix) => string.IsNullOrEmpty(suffix) ? _root : (_root + ":" + suffix);

        public bool Blp(string suffix, float baseChance01, float bonusPerFail01 = 0.03f, float maxChance01 = 0.95f)
            => _rng.TestBernoulliBlp(_sid, Key(suffix), baseChance01, bonusPerFail01, maxChance01);

        public bool Rppm(string suffix, float rppm, float hasteMult = 1f)
            => _rng.TestRppm(_sid, Key(suffix), rppm, _timer.NowSeconds, hasteMult);

        public bool IcdReady(string suffix, double icdSeconds)
            => _rng.TestIcd(_sid, Key(suffix), _timer.NowSeconds, icdSeconds);

        public double CooldownRemaining(string suffix, double icdSeconds)
            => _rng.CooldownRemaining(_sid, Key(suffix), _timer.NowSeconds, icdSeconds);

        public void ResetKey(string suffix) => _rng.ResetKey(_sid, Key(suffix));

        public void ResetAllForSid() => _rng.ResetSid(_sid);
    }
}
