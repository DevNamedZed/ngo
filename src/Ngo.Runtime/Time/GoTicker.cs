using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Time
{
    // time.Ticker — struct type
    [GoType("struct", Name = "Ticker", Package = "time")]
    public sealed class GoTicker
    {
        [GoField(Name = "C")]
        public Channel<GoTimeValue> C_chan;

        private System.Threading.Timer? _timer;
        private readonly object _lock = new object();
        private bool _stopped;

        public GoTicker(long durationNanoseconds)
        {
            C_chan = new Channel<GoTimeValue>(1);
            var ms = durationNanoseconds / 1_000_000;
            if (ms < 1)
            {
                ms = 1;
            }
            _timer = new System.Threading.Timer(OnTick, null, (long)ms, (long)ms);
        }

        private void OnTick(object? state)
        {
            lock (_lock)
            {
                if (_stopped)
                {
                    return;
                }
            }
            C_chan.TrySend(GoTime.Now());
        }

        [GoMethod]
        public void Stop()
        {
            lock (_lock)
            {
                _stopped = true;
                _timer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                _timer?.Dispose();
                _timer = null;
            }
        }

        [GoMethod]
        public void Reset([GoParam("Duration")] long d)
        {
            lock (_lock)
            {
                _stopped = false;
                var ms = d / 1_000_000;
                if (ms < 1)
                {
                    ms = 1;
                }
                if (_timer != null)
                {
                    _timer.Change((long)ms, (long)ms);
                }
                else
                {
                    _timer = new System.Threading.Timer(OnTick, null, (long)ms, (long)ms);
                }
            }
        }
    }
}
