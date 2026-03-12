using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync.Atomic
{
    [GoType("struct", Name = "Pointer", Package = "sync/atomic")]
    public class Pointer<T> where T : class
    {
        private T? _value;

        [GoMethod]
        public void Store(T? v) { _value = v; }

        [GoMethod]
        public T? Load() { return _value; }

        [GoMethod]
        public bool CompareAndSwap(T? old, T? @new)
        {
            if (object.ReferenceEquals(_value, old)) { _value = @new; return true; }
            return false;
        }

        [GoMethod]
        public T? Swap(T? @new)
        {
            var old = _value;
            _value = @new;
            return old;
        }
    }
}
