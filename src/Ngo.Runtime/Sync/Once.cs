using System;
using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync
{
    [GoType("struct", Name = "Once", Package = "sync")]
    public sealed class Once
    {
        private int _done;
        private readonly object _lock = new();

        [GoMethod]
        public void Do(Action f)
        {
            if (Interlocked.CompareExchange(ref _done, 1, 0) == 0)
            {
                lock (_lock)
                {
                    f();
                }
            }
        }
    }
}
