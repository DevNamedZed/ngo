using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Log.Slog
{
    // slog.Value struct
    [GoType("struct", Name = "Value", Package = "log/slog")]
    public struct GoValue
    {
        private object? _value;
        private long _kind;

        public GoValue(object? value) : this(value, Package.KindAny) { }

        internal GoValue(object? value, long kind)
        {
            _value = value;
            _kind = kind;
        }

        [GoMethod]
        public string String() => _value?.ToString() ?? "";

        [GoMethod]
        public long Kind() => _kind;

        [GoMethod]
        public GoValue Resolve() => this;

        [GoMethod]
        public object? Any() => _value;

        [GoMethod]
        public object Time() => _value ?? new object();

        [GoMethod]
        public long Duration() => _value is long l ? l : 0;

        [GoMethod]
        public long Int64() => _value is long l ? l : 0;

        [GoMethod]
        public ulong Uint64() => _value is ulong u ? u : 0;

        [GoMethod]
        public double Float64() => _value is double d ? d : 0;

        [GoMethod]
        public bool Bool() => _value is bool b && b;

        [GoMethod]
        public Slice<GoAttr> Group()
        {
            if (_value is Slice<GoAttr> attrs)
            {
                return attrs;
            }
            return new Slice<GoAttr>();
        }

        [GoMethod]
        public bool Equal(GoValue other) => Equals(_value, other._value) && _kind == other._kind;
    }
}
