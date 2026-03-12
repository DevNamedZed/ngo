using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync.Atomic
{
    [GoType("struct", Name = "Int32", Package = "sync/atomic")]
    public class Int32
    {
        private long _value;

        [GoMethod]
        public long Load() { return Interlocked.Read(ref _value); }

        [GoMethod]
        public void Store(long val) { Interlocked.Exchange(ref _value, val); }

        [GoMethod]
        public long Add(long delta) { return Interlocked.Add(ref _value, delta); }

        [GoMethod]
        public bool CompareAndSwap(long old, long @new)
        {
            return Interlocked.CompareExchange(ref _value, @new, old) == old;
        }

        [GoMethod]
        public long Swap(long @new) { return Interlocked.Exchange(ref _value, @new); }
    }
}
