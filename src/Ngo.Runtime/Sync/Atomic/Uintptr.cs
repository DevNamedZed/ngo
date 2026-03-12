using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync.Atomic
{
    [GoType("struct", Name = "Uintptr", Package = "sync/atomic")]
    public class Uintptr
    {
        private long _value;

        [GoMethod]
        public long Load() { return _value; }

        [GoMethod]
        public void Store(long val) { _value = val; }

        [GoMethod]
        public long Add(long delta) { return Interlocked.Add(ref _value, delta); }
    }
}
