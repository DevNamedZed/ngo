using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync.Atomic
{
    [GoType("struct", Name = "Bool", Package = "sync/atomic")]
    public class Bool
    {
        private long _value;

        [GoMethod]
        public bool Load() { return Interlocked.Read(ref _value) != 0; }

        [GoMethod]
        public void Store(bool val) { Interlocked.Exchange(ref _value, val ? 1 : 0); }

        [GoMethod]
        public bool Swap(bool @new)
        {
            return Interlocked.Exchange(ref _value, @new ? 1 : 0) != 0;
        }

        [GoMethod]
        public bool CompareAndSwap(bool old, bool @new)
        {
            return Interlocked.CompareExchange(ref _value, @new ? 1 : 0, old ? 1 : 0) == (old ? 1 : 0);
        }
    }
}
