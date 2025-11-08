using System.Diagnostics;
using System.Threading;

namespace WarcraftCS2.Spells.Systems.Core.Runtime
{
    // Лёгкий таймер для единой шкалы времени и "эпох" (тиков).
    public sealed class ProcTimer
    {
        private readonly Stopwatch _sw = Stopwatch.StartNew();
        private long _epoch;

        public double NowSeconds => _sw.Elapsed.TotalSeconds;

        public long Epoch => Interlocked.Read(ref _epoch);

        public long Tick() => Interlocked.Increment(ref _epoch);

        public void SetEpoch(long value) => Interlocked.Exchange(ref _epoch, value);

        public void Reset()
        {
            _sw.Restart();
            Interlocked.Exchange(ref _epoch, 0);
        }
    }
}