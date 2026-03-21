using System.Numerics;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Math.Big
{
    [GoType("struct", Name = "Rat", Package = "math/big", Pointer = true)]
    public class GoRat
    {
        internal BigInteger _value;
        internal BigInteger _denom = BigInteger.One;

        [GoMethod]
        public (GoRat, bool) SetString(string s)
        {
            var parts = s.Split('/');
            if (parts.Length == 2 && BigInteger.TryParse(parts[0], out var num) && BigInteger.TryParse(parts[1], out var den))
            {
                _value = num;
                _denom = den;
                return (this, true);
            }
            if (BigInteger.TryParse(s, out var n))
            {
                _value = n;
                _denom = BigInteger.One;
                return (this, true);
            }
            return (null!, false);
        }

        [GoMethod]
        public string FloatString(long prec) => ((double)_value / (double)_denom).ToString($"F{prec}");

        [GoMethod]
        public string String() => _denom == BigInteger.One ? _value.ToString() : $"{_value}/{_denom}";

        [GoMethod]
        public string RatString() => $"{_value}/{_denom}";

        [GoMethod]
        public GoInt Num() { var i = new GoInt(); i._value = _value; return i; }

        [GoMethod]
        public GoInt Denom() { var i = new GoInt(); i._value = _denom; return i; }

        [GoMethod]
        public (double, bool) Float64()
        {
            var result = (double)_value / (double)_denom;
            return (result, !double.IsInfinity(result));
        }

        [GoMethod]
        public GoRat SetInt(GoInt x) { _value = x._value; _denom = BigInteger.One; return this; }

        [GoMethod]
        public GoRat Mul(GoRat x, GoRat y)
        {
            _value = x._value * y._value;
            _denom = x._denom * y._denom;
            return this;
        }

        [GoMethod]
        public GoRat Add(GoRat x, GoRat y)
        {
            _value = x._value * y._denom + y._value * x._denom;
            _denom = x._denom * y._denom;
            return this;
        }

        [GoMethod]
        public GoRat Sub(GoRat x, GoRat y)
        {
            _value = x._value * y._denom - y._value * x._denom;
            _denom = x._denom * y._denom;
            return this;
        }

        [GoMethod]
        public GoRat Quo(GoRat x, GoRat y)
        {
            _value = x._value * y._denom;
            _denom = x._denom * y._value;
            return this;
        }

        [GoMethod]
        public GoRat SetFloat64(double f)
        {
            // Simple approximation
            _value = new BigInteger(f * 1e15);
            _denom = new BigInteger(1e15);
            return this;
        }

        [GoMethod]
        public long Cmp(GoRat y)
        {
            var left = _value * y._denom;
            var right = y._value * _denom;
            return left.CompareTo(right);
        }

        [GoMethod]
        public long Sign() => _value.Sign * _denom.Sign;

        [GoMethod]
        public GoRat SetInt64(long x) { _value = new BigInteger(x); _denom = BigInteger.One; return this; }

        [GoMethod]
        public (float, bool) Float32()
        {
            var result = (float)((double)_value / (double)_denom);
            return (result, !float.IsInfinity(result));
        }

        [GoMethod]
        public GoRat Set(GoRat x) { _value = x._value; _denom = x._denom; return this; }

        [GoMethod]
        public GoRat SetFrac(GoInt a, GoInt b) { _value = a._value; _denom = b._value; return this; }

        [GoMethod]
        public bool IsInt() => _denom == BigInteger.One || (_value % _denom == BigInteger.Zero);

        [GoMethod]
        public GoRat Inv(GoRat x) { _value = x._denom; _denom = x._value; return this; }

        [GoMethod]
        public GoRat Neg(GoRat x) { _value = -x._value; _denom = x._denom; return this; }

        [GoMethod]
        public GoRat Abs(GoRat x) { _value = BigInteger.Abs(x._value); _denom = BigInteger.Abs(x._denom); return this; }
    }
}
