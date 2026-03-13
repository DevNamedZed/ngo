using System.Threading;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync.Atomic
{
    [GoType("struct", Name = "Value", Package = "sync/atomic")]
    public class Value
    {
        private object? _value;

        [GoMethod]
        public void Store(object? v)
        {
            Interlocked.Exchange(ref _value, v);
        }

        [GoMethod]
        public object? Load()
        {
            return Interlocked.CompareExchange(ref _value, null, null);
        }

        [GoMethod]
        public bool CompareAndSwap(object? old, object? @new)
        {
            return Interlocked.CompareExchange(ref _value, @new, old) == old;
        }

        [GoMethod]
        public object? Swap(object? @new)
        {
            return Interlocked.Exchange(ref _value, @new);
        }
    }
}
