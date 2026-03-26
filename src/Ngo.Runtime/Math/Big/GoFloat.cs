using System.Numerics;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Math.Big
{
    [GoType("struct", Name = "Float", Package = "math/big", Pointer = true)]
    public class GoFloat
    {
        internal double _value;
        internal uint _prec = 53;

        [GoMethod]
        public GoFloat SetFloat64(double x) { _value = x; return this; }

        [GoMethod]
        public (double, object) Float64() => (_value, null!);

        [GoMethod]
        public string String() => _value.ToString("G");

        [GoMethod]
        public string Text(byte format, long prec) => _value.ToString($"G{prec}");

        [GoMethod]
        public long Cmp(GoFloat y) => _value.CompareTo(y._value);

        [GoMethod]
        public GoFloat SetPrec(ulong prec) { _prec = (uint)prec; return this; }

        [GoMethod]
        public GoFloat Add(GoFloat x, GoFloat y) { _value = x._value + y._value; return this; }

        [GoMethod]
        public GoFloat Sub(GoFloat x, GoFloat y) { _value = x._value - y._value; return this; }

        [GoMethod]
        public GoFloat Mul(GoFloat x, GoFloat y) { _value = x._value * y._value; return this; }

        [GoMethod]
        public GoFloat Quo(GoFloat x, GoFloat y) { _value = x._value / y._value; return this; }

        [GoMethod]
        public long Sign() => System.Math.Sign(_value);

        [GoMethod]
        public GoFloat Abs(GoFloat x) { _value = System.Math.Abs(x._value); return this; }

        [GoMethod]
        public GoFloat Neg(GoFloat x) { _value = -x._value; return this; }

        [GoMethod]
        public GoFloat SetInt(GoInt x) { _value = (double)x._value; return this; }

        [GoMethod]
        public (GoInt, object) Int(GoInt z)
        {
            z ??= new GoInt();
            z._value = new BigInteger(_value);
            return (z, null!);
        }

        [GoMethod]
        public (float, object) Float32() => ((float)_value, null!);

        [GoMethod]
        public bool IsInf() => double.IsInfinity(_value);

        [GoMethod]
        public bool IsInt() => _value == System.Math.Truncate(_value) && !double.IsInfinity(_value) && !double.IsNaN(_value);

        [GoMethod]
        public ulong Prec() => _prec;

        [GoMethod]
        public ulong MinPrec() => 53;

        [GoMethod]
        public GoFloat Copy(GoFloat x) { _value = x._value; _prec = x._prec; return this; }

        [GoMethod]
        public GoFloat SetInf(bool signbit) { _value = signbit ? double.NegativeInfinity : double.PositiveInfinity; return this; }

        [GoMethod]
        public GoFloat Set(GoFloat x) { _value = x._value; _prec = x._prec; return this; }

        [GoMethod]
        public GoFloat SetInt64(long x) { _value = x; return this; }

        [GoMethod]
        public GoFloat SetUint64(ulong x) { _value = x; return this; }

        [GoMethod]
        public (long, object) Int64() => ((long)_value, null!);

        [GoMethod]
        public (GoFloat, long, object) Parse(string s, long @base)
        {
            if (double.TryParse(s, out var result))
            {
                _value = result;
                return (this, 10, null!);
            }
            return (this, 0, (object)"failed to parse");
        }

        [GoMethod]
        public GoFloat SetRat(GoRat x)
        {
            _value = (double)x._value / (double)x._denom;
            return this;
        }

        [GoMethod]
        public (GoFloat, bool) SetString(string s)
        {
            if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result))
            {
                _value = result;
                return (this, true);
            }
            return (null!, false);
        }

        [GoMethod]
        public long MantExp([GoParam("*big.Float")] GoFloat mant)
        {
            if (_value == 0) { if (mant != null) mant._value = 0; return 0; }
            var bits = System.BitConverter.DoubleToInt64Bits(_value);
            var exp = (int)((bits >> 52) & 0x7FF) - 1023;
            if (mant != null)
            {
                mant._value = _value / System.Math.Pow(2, exp);
                mant._prec = _prec;
            }
            return exp;
        }

        [GoMethod]
        public GoFloat SetMantExp(GoFloat mant, long exp)
        {
            _value = mant._value * System.Math.Pow(2, exp);
            return this;
        }

        [GoMethod]
        public GoFloat SetMode(object mode) { return this; } // RoundingMode stub

        [GoMethod]
        public object Mode() { return (object)0; } // RoundingMode stub

        [GoMethod]
        public (GoRat, object) Rat([GoParam("*big.Rat")] GoRat z)
        {
            z ??= new GoRat();
            // Simple conversion: approximate as rational
            z._value = new BigInteger(_value * 1e15);
            z._denom = new BigInteger(1e15);
            return (z, null!);
        }

        [GoMethod]
        public bool Signbit()
        {
            return double.IsNegativeInfinity(_value) || (_value < 0) || (1.0 / _value == double.NegativeInfinity);
        }

        [GoMethod]
        [return: GoReturn("uint64", "Accuracy")]
        public (ulong, long) Uint64()
        {
            if (_value < 0 || double.IsNaN(_value))
            {
                return (0, -1); // Below — Accuracy.Below
            }
            if (double.IsPositiveInfinity(_value) || _value > ulong.MaxValue)
            {
                return (ulong.MaxValue, 1); // Above — Accuracy.Above
            }
            return ((ulong)_value, 0); // Exact — Accuracy.Exact
        }
    }
}
