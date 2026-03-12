using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Sync.Atomic
{
    [GoType("struct", Name = "Value", Package = "sync/atomic")]
    public class Value
    {
        private object? _value;

        [GoMethod]
        public void Store(object? v) { _value = v; }

        [GoMethod]
        public object? Load() { return _value; }

        [GoMethod]
        public bool CompareAndSwap(object? old, object? @new)
        {
            if (object.Equals(_value, old)) { _value = @new; return true; }
            return false;
        }

        [GoMethod]
        public object? Swap(object? @new)
        {
            var old = _value;
            _value = @new;
            return old;
        }
    }
}
