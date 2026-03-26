using System.Numerics;
using Ngo.Runtime.Discovery;

namespace Ngo.Runtime.Math.Big
{
    [GoType("struct", Name = "Int", Package = "math/big", Pointer = true)]
    public class GoInt
    {
        internal BigInteger _value;

        [GoMethod]
        public long Int64() => (long)_value;

        [GoMethod]
        public string String() => _value.ToString();

        [GoMethod]
        public GoInt SetInt64(long x) { _value = new BigInteger(x); return this; }

        [GoMethod]
        public (GoInt, bool) SetString(string s, long @base)
        {
            if (BigInteger.TryParse(s, out var result))
            {
                _value = result;
                return (this, true);
            }
            return (null!, false);
        }

        [GoMethod]
        public GoInt Add(GoInt x, GoInt y) { _value = x._value + y._value; return this; }

        [GoMethod]
        public GoInt Sub(GoInt x, GoInt y) { _value = x._value - y._value; return this; }

        [GoMethod]
        public GoInt Mul(GoInt x, GoInt y) { _value = x._value * y._value; return this; }

        [GoMethod]
        public GoInt Div(GoInt x, GoInt y) { _value = BigInteger.Divide(x._value, y._value); return this; }

        [GoMethod]
        public long Cmp(GoInt y) => _value.CompareTo(y._value);

        [GoMethod]
        public Slice<byte> Bytes() => new Slice<byte>(_value.ToByteArray(isUnsigned: true, isBigEndian: true));

        [GoMethod]
        public long BitLen() => (long)_value.GetBitLength();

        [GoMethod]
        public long Sign() => _value.Sign;

        [GoMethod]
        public GoInt Abs(GoInt x) { _value = BigInteger.Abs(x._value); return this; }

        [GoMethod]
        public GoInt Set(GoInt x) { _value = x._value; return this; }

        [GoMethod]
        public (GoInt, GoInt) DivMod(GoInt x, GoInt y, GoInt m)
        {
            _value = BigInteger.DivRem(x._value, y._value, out var rem);
            m._value = rem;
            return (this, m);
        }

        [GoMethod]
        public GoInt Mod(GoInt x, GoInt y) { _value = x._value % y._value; return this; }

        [GoMethod]
        public GoInt Exp(GoInt x, GoInt y, GoInt m)
        {
            _value = m != null ? BigInteger.ModPow(x._value, y._value, m._value) : BigInteger.Pow(x._value, (int)y._value);
            return this;
        }

        [GoMethod]
        public GoInt Neg(GoInt x) { _value = -x._value; return this; }

        [GoMethod]
        public GoInt Lsh(GoInt x, ulong n) { _value = x._value << (int)n; return this; }

        [GoMethod]
        public GoInt Rsh(GoInt x, ulong n) { _value = x._value >> (int)n; return this; }

        [GoMethod]
        public bool IsInt64() => _value >= long.MinValue && _value <= long.MaxValue;

        [GoMethod]
        public GoInt SetBytes(Slice<byte> buf)
        {
            _value = new BigInteger(buf.AsSpan().ToArray(), isUnsigned: true, isBigEndian: true);
            return this;
        }

        [GoMethod]
        public GoInt SetUint64(ulong x) { _value = new BigInteger(x); return this; }

        [GoMethod]
        public ulong Uint64() => (ulong)_value;

        [GoMethod]
        public bool IsUint64() => _value >= 0 && _value <= ulong.MaxValue;

        [GoMethod]
        public (GoInt, GoInt) QuoRem(GoInt x, GoInt y, GoInt r)
        {
            _value = BigInteger.DivRem(x._value, y._value, out var rem);
            r._value = rem;
            return (this, r);
        }

        [GoMethod]
        public GoInt Quo(GoInt x, GoInt y)
        {
            // Truncated division (same as Div for positive numbers, differs for negative)
            _value = BigInteger.Divide(x._value, y._value);
            return this;
        }

        [GoMethod]
        public GoInt Or(GoInt x, GoInt y)
        {
            _value = x._value | y._value;
            return this;
        }

        [GoMethod]
        public GoInt And(GoInt x, GoInt y)
        {
            _value = x._value & y._value;
            return this;
        }

        [GoMethod]
        public GoInt ModInverse(GoInt g, GoInt n)
        {
            // Extended Euclidean algorithm for modular inverse
            var gcd = BigInteger.GreatestCommonDivisor(g._value, n._value);
            if (gcd != BigInteger.One)
            {
                _value = BigInteger.Zero;
                return this;
            }
            // Use the formula: g^(n-2) mod n for prime n, or extended GCD otherwise
            // .NET doesn't have ModInverse directly, but ModPow with exponent n-2 works for prime n
            // For general case, use iterative extended GCD
            BigInteger a = g._value % n._value;
            if (a < 0) a += n._value;
            BigInteger t = 0, newt = 1;
            BigInteger r = n._value, newr = a;
            while (newr != 0)
            {
                var quotient = BigInteger.Divide(r, newr);
                (t, newt) = (newt, t - quotient * newt);
                (r, newr) = (newr, r - quotient * newr);
            }
            if (t < 0) t += n._value;
            _value = t;
            return this;
        }

        [GoMethod]
        public bool ProbablyPrime(long n)
        {
            if (_value < 2)
            {
                return false;
            }
            if (_value == 2 || _value == 3)
            {
                return true;
            }
            if (_value.IsEven)
            {
                return false;
            }

            // Trial division for small primes
            int[] smallPrimes = { 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47 };
            foreach (int p in smallPrimes)
            {
                if (_value == p)
                {
                    return true;
                }
                if (_value % p == 0)
                {
                    return false;
                }
            }

            // Miller-Rabin primality test
            BigInteger d = _value - 1;
            int r = 0;
            while (d.IsEven)
            {
                d >>= 1;
                r++;
            }

            BigInteger[] witnesses = { 2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37 };
            foreach (var a in witnesses)
            {
                if (a >= _value)
                {
                    continue;
                }
                BigInteger x = BigInteger.ModPow(a, d, _value);
                if (x == 1 || x == _value - 1)
                {
                    continue;
                }
                bool composite = true;
                for (int i = 0; i < r - 1; i++)
                {
                    x = BigInteger.ModPow(x, 2, _value);
                    if (x == _value - 1)
                    {
                        composite = false;
                        break;
                    }
                }
                if (composite)
                {
                    return false;
                }
            }
            return true;
        }

        [GoMethod]
        public Slice<byte> FillBytes(Slice<byte> buf)
        {
            var bytes = _value.ToByteArray(isUnsigned: true, isBigEndian: true);
            // Zero-fill the buffer first
            for (int i = 0; i < buf.Len; i++)
                buf[i] = 0;
            // Copy value bytes right-aligned into buf
            var offset = buf.Len - bytes.Length;
            if (offset < 0) offset = 0;
            var copyLen = System.Math.Min(bytes.Length, buf.Len);
            for (int i = 0; i < copyLen; i++)
                buf[(int)offset + i] = bytes[bytes.Length - copyLen + i];
            return buf;
        }

        [GoMethod]
        public ulong Bit(long i)
        {
            if (i < 0) return 0;
            return (_value >> (int)i & BigInteger.One) == BigInteger.One ? 1UL : 0UL;
        }

        [GoMethod]
        public Slice<ulong> Bits()
        {
            // Return the absolute value as a slice of Words (uint in Go, but mapped as ulong)
            var bytes = BigInteger.Abs(_value).ToByteArray(isUnsigned: true, isBigEndian: false);
            var wordCount = (bytes.Length + 7) / 8;
            var words = new ulong[wordCount];
            for (int i = 0; i < bytes.Length; i++)
                words[i / 8] |= (ulong)bytes[i] << ((i % 8) * 8);
            return new Slice<ulong>(words);
        }

        [GoMethod]
        public GoInt SetBits(Slice<ulong> abs)
        {
            _value = BigInteger.Zero;
            for (int i = 0; i < abs.Len; i++)
                _value |= new BigInteger(abs[i]) << (i * 64);
            return this;
        }

        [GoMethod]
        public GoInt Xor(GoInt x, GoInt y) { _value = x._value ^ y._value; return this; }

        [GoMethod]
        public GoInt Not(GoInt x) { _value = ~x._value; return this; }

        [GoMethod]
        public GoInt AndNot(GoInt x, GoInt y) { _value = x._value & ~y._value; return this; }

        [GoMethod]
        public GoInt Rem(GoInt x, GoInt y) { _value = x._value % y._value; return this; }

        [GoMethod]
        public GoInt GCD(GoInt x, GoInt y, GoInt a, GoInt b) { _value = BigInteger.GreatestCommonDivisor(a._value, b._value); return this; }

        [GoMethod]
        public GoInt Sqrt(GoInt x)
        {
            if (x._value < 0) { _value = BigInteger.Zero; return this; }
            if (x._value == 0) { _value = BigInteger.Zero; return this; }
            var n = x._value;
            var guess = n >> ((int)n.GetBitLength() / 2);
            if (guess == 0) guess = 1;
            for (int i = 0; i < 256; i++)
            {
                var next = (guess + n / guess) >> 1;
                if (next >= guess) break;
                guess = next;
            }
            _value = guess;
            return this;
        }

        [GoMethod]
        [return: GoReturn("[]byte")]
        public Slice<byte> Append(Slice<byte> buf, long @base)
        {
            var s = @base == 16 ? _value.ToString("x") : _value.ToString();
            var bytes = System.Text.Encoding.ASCII.GetBytes(s);
            return Slice<byte>.Append(buf, new Slice<byte>(bytes));
        }

        [GoMethod]
        public GoInt ModSqrt(GoInt x, GoInt p)
        {
            // Tonelli-Shanks is complex; provide a basic stub
            // For p ≡ 3 (mod 4), sqrt = x^((p+1)/4) mod p
            var xv = x._value % p._value;
            if (xv < 0) xv += p._value;
            if (xv == 0) { _value = BigInteger.Zero; return this; }
            if ((p._value & 3) == 3)
            {
                _value = BigInteger.ModPow(xv, (p._value + 1) / 4, p._value);
                return this;
            }
            // General case: return zero as stub
            _value = BigInteger.Zero;
            return this;
        }

        [GoMethod]
        public GoInt Binomial(long n, long k)
        {
            if (k < 0 || k > n) { _value = BigInteger.Zero; return this; }
            if (k == 0 || k == n) { _value = BigInteger.One; return this; }
            var result = BigInteger.One;
            for (long i = 0; i < k; i++)
            {
                result = result * (n - i) / (i + 1);
            }
            _value = result;
            return this;
        }

        [GoMethod]
        public long CmpAbs(GoInt y) => BigInteger.Abs(_value).CompareTo(BigInteger.Abs(y._value));

        [GoMethod]
        public void Format(object state, long verb)
        {
            string formatted = verb switch
            {
                'd' => _value.ToString(),
                'x' => _value.ToString("x"),
                'X' => _value.ToString("X"),
                'o' => FormatToRadix(_value, 8),
                'b' => FormatToRadix(_value, 2),
                's' => _value.ToString(),
                'v' => _value.ToString(),
                _ => _value.ToString(),
            };
            // Write to state via reflection
            var writeMethod = state?.GetType().GetMethod("Write");
            if (writeMethod != null)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(formatted);
                writeMethod.Invoke(state, new object[] { new Slice<byte>(bytes) });
            }
        }

        private static string FormatToRadix(BigInteger value, int radix)
        {
            if (value == 0)
            {
                return "0";
            }
            bool negative = value < 0;
            if (negative)
            {
                value = -value;
            }
            var chars = new System.Collections.Generic.List<char>();
            while (value > 0)
            {
                int remainder = (int)(value % radix);
                chars.Add(remainder < 10 ? (char)('0' + remainder) : (char)('a' + remainder - 10));
                value /= radix;
            }
            if (negative)
            {
                chars.Add('-');
            }
            chars.Reverse();
            return new string(chars.ToArray());
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, string) GobEncode()
        {
            var bytes = _value.ToByteArray(isUnsigned: false, isBigEndian: true);
            return (new Slice<byte>(bytes), "");
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string GobDecode(Slice<byte> buf)
        {
            _value = new BigInteger(buf.AsSpan().ToArray(), isUnsigned: false, isBigEndian: true);
            return "";
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, string) MarshalJSON()
        {
            var s = _value.ToString();
            return (new Slice<byte>(System.Text.Encoding.UTF8.GetBytes(s)), "");
        }

        [GoMethod]
        [return: GoReturn("[]byte", "error")]
        public (Slice<byte>, string) MarshalText()
        {
            var s = _value.ToString();
            return (new Slice<byte>(System.Text.Encoding.UTF8.GetBytes(s)), "");
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string UnmarshalJSON(Slice<byte> text)
        {
            var s = System.Text.Encoding.UTF8.GetString(text.AsSpan().ToArray());
            if (BigInteger.TryParse(s, out var result))
            {
                _value = result;
                return "";
            }
            return "math/big: cannot unmarshal " + s + " into Go value of type *big.Int";
        }

        [GoMethod]
        [return: GoReturn("error")]
        public string UnmarshalText(Slice<byte> text)
        {
            var s = System.Text.Encoding.UTF8.GetString(text.AsSpan().ToArray());
            if (BigInteger.TryParse(s, out var result))
            {
                _value = result;
                return "";
            }
            return "math/big: cannot unmarshal " + s + " into Go value of type *big.Int";
        }

        [GoMethod]
        public GoInt MulRange(long a, long b)
        {
            if (a > b) { _value = BigInteger.One; return this; }
            var result = BigInteger.One;
            for (long i = a; i <= b; i++)
            {
                result *= i;
            }
            _value = result;
            return this;
        }

        [GoMethod]
        public GoInt Rand(object rng, GoInt n)
        {
            if (n._value <= 0)
            {
                _value = BigInteger.Zero;
                return this;
            }
            var byteCount = n._value.GetByteCount(isUnsigned: true);
            var bytes = new byte[byteCount + 1]; // +1 to ensure positive
            var random = new System.Random();
            random.NextBytes(bytes);
            bytes[bytes.Length - 1] = 0; // ensure positive
            var candidate = new BigInteger(bytes, isUnsigned: true);
            _value = candidate % n._value;
            return this;
        }

        [GoMethod]
        [return: GoReturn("error")]
        public object? Scan([GoParam("fmt.ScanState")] object? state, [GoParam("rune")] long ch)
        {
            return null;
        }

        [GoMethod]
        public GoInt SetBit(GoInt x, long i, ulong b)
        {
            if (b == 0)
            {
                _value = x._value & ~(BigInteger.One << (int)i);
            }
            else
            {
                _value = x._value | (BigInteger.One << (int)i);
            }
            return this;
        }

        [GoMethod]
        public string Text(long @base)
        {
            return @base switch
            {
                16 => _value.ToString("x"),
                10 => _value.ToString(),
                8 => ConvertToBase(_value, 8),
                2 => ConvertToBase(_value, 2),
                _ => _value.ToString(),
            };
        }

        [GoMethod]
        public ulong TrailingZeroBits()
        {
            if (_value == 0) return 0;
            var abs = BigInteger.Abs(_value);
            ulong count = 0;
            while ((abs & 1) == 0)
            {
                abs >>= 1;
                count++;
            }
            return count;
        }

        private static string ConvertToBase(BigInteger value, int radix)
        {
            if (value == 0) return "0";
            var neg = value < 0;
            value = BigInteger.Abs(value);
            var chars = new System.Collections.Generic.List<char>();
            while (value > 0)
            {
                chars.Add("0123456789abcdef"[(int)(value % radix)]);
                value /= radix;
            }
            if (neg) chars.Add('-');
            chars.Reverse();
            return new string(chars.ToArray());
        }

    }
}

