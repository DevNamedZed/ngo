using System;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Time
{
    // time.Timer — struct type
    [GoType("struct", Name = "Timer", Package = "time")]
    public sealed class GoTimer
    {
        [GoField(Name = "C", Type = "<-chan Time")]
        public Channel<GoTimeValue> C { get; set; } = new Channel<GoTimeValue>(1);

        private System.Threading.Timer? _timer;
        private readonly object _lock = new object();
        private bool _stopped;

        public GoTimer(long durationNanoseconds)
        {
            var ms = durationNanoseconds / 1_000_000;
            if (ms < 1)
            {
                ms = 1;
            }
            _timer = new System.Threading.Timer(OnFired, null, (long)ms, System.Threading.Timeout.Infinite);
        }

        private void OnFired(object? state)
        {
            lock (_lock)
            {
                if (_stopped)
                {
                    return;
                }
            }
            C.TrySend(GoTime.Now());
        }

        [GoMethod]
        public bool Stop()
        {
            lock (_lock)
            {
                if (_stopped)
                {
                    return false;
                }
                _stopped = true;
                _timer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
                _timer?.Dispose();
                _timer = null;
                return true;
            }
        }

        [GoMethod]
        public bool Reset([GoParam("Duration")] long d)
        {
            lock (_lock)
            {
                bool wasActive = !_stopped;
                _stopped = false;
                var ms = d / 1_000_000;
                if (ms < 1)
                {
                    ms = 1;
                }
                if (_timer != null)
                {
                    _timer.Change((long)ms, System.Threading.Timeout.Infinite);
                }
                else
                {
                    _timer = new System.Threading.Timer(OnFired, null, (long)ms, System.Threading.Timeout.Infinite);
                }
                return wasActive;
            }
        }
    }
}
