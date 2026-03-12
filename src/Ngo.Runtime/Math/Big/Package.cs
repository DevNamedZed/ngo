using System.Numerics;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Math.Big
{
    /// <summary>
    /// Runtime support for Go's math/big package.
    /// </summary>
    [GoPackage("math/big")]
    public static class Package
    {
        // Accuracy constants
        [GoConst]
        public const long Exact = 0;
        [GoConst]
        public const long Above = 1;
        [GoConst]
        public const long Below = -1;

        [GoFunc]
        public static GoInt NewInt(long x)
        {
            var i = new GoInt();
            i.SetInt64(x);
            return i;
        }

        [GoFunc]
        public static GoFloat NewFloat(double x)
        {
            var f = new GoFloat();
            f.SetFloat64(x);
            return f;
        }

        [GoFunc]
        public static GoRat NewRat(long a, long b)
        {
            var r = new GoRat();
            r._value = new BigInteger(a);
            r._denom = new BigInteger(b);
            return r;
        }

        [GoFunc]
        public static (GoFloat, long, object) ParseFloat(string s, long @base, ulong prec, object mode)
        {
            var f = new GoFloat();
            var (result, b, err) = f.Parse(s, @base);
            return (result, b, err);
        }

        // RoundingMode constants
        [GoConst(Type = "big.RoundingMode")]
        public const byte ToNearestEven = 0;
        [GoConst(Type = "big.RoundingMode")]
        public const byte ToNearestAway = 1;
        [GoConst(Type = "big.RoundingMode")]
        public const byte ToZero = 2;
        [GoConst(Type = "big.RoundingMode")]
        public const byte AwayFromZero = 3;
        [GoConst(Type = "big.RoundingMode")]
        public const byte ToNegativeInf = 4;
        [GoConst(Type = "big.RoundingMode")]
        public const byte ToPositiveInf = 5;

        // RoundingMode type
        [GoType("named", Name = "RoundingMode", Package = "math/big", Underlying = "byte")]
        public struct GoRoundingMode
        {
            public byte Value;
            public GoRoundingMode(byte v) { Value = v; }

            [GoMethod]
            public string String() => Value switch
            {
                0 => "ToNearestEven",
                1 => "ToNearestAway",
                2 => "ToZero",
                3 => "AwayFromZero",
                4 => "ToNegativeInf",
                5 => "ToPositiveInf",
                _ => $"RoundingMode({Value})"
            };
        }

        // Word type — alias for uint (platform word size)
        [GoType("named", Name = "Word", Package = "math/big", Underlying = "uint")]
        public struct GoWord
        {
            public ulong Value;
            public GoWord(ulong v) { Value = v; }
        }
    }
}
