using Ngo.Runtime.Discovery;
using Ngo.Runtime.Io;

namespace Ngo.Runtime.Hash.Fnv
{
    [GoPackage("hash/fnv")]
    public static class Package
    {
        // FNV constants
        private const uint Offset32 = 2166136261;
        private const uint Prime32 = 16777619;
        private const ulong Offset64 = 14695981039346656037;
        private const ulong Prime64 = 1099511628211;

        // fnv.New32() hash.Hash32
        [GoFunc]
        [return: GoReturn("hash.Hash32")]
        public static object New32()
        {
            return new Fnv32Hash(false);
        }

        // fnv.New32a() hash.Hash32
        [GoFunc]
        [return: GoReturn("hash.Hash32")]
        public static object New32a()
        {
            return new Fnv32Hash(true);
        }

        // fnv.New64() hash.Hash64
        [GoFunc]
        [return: GoReturn("hash.Hash64")]
        public static object New64()
        {
            return new Fnv64Hash(false);
        }

        // fnv.New64a() hash.Hash64
        [GoFunc]
        [return: GoReturn("hash.Hash64")]
        public static object New64a()
        {
            return new Fnv64Hash(true);
        }

        // fnv.New128() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static object New128()
        {
            return new Fnv128Hash(false);
        }

        // fnv.New128a() hash.Hash
        [GoFunc]
        [return: GoReturn("hash.Hash")]
        public static object New128a()
        {
            return new Fnv128Hash(true);
        }
    }

    internal class Fnv32Hash : IGoHash32
    {
        private const uint Offset32 = 2166136261;
        private const uint Prime32 = 16777619;
        private readonly bool _isA;
        private uint _hash;

        public Fnv32Hash(bool isA)
        {
            _isA = isA;
            _hash = Offset32;
        }

        public (int, string) Write(Slice<byte> p)
        {
            for (int i = 0; i < p.Len; i++)
            {
                if (_isA)
                {
                    _hash ^= p[i];
                    _hash *= Prime32;
                }
                else
                {
                    _hash *= Prime32;
                    _hash ^= p[i];
                }
            }
            return (p.Len, null!);
        }

        public Slice<byte> Sum(Slice<byte> b)
        {
            uint s = _hash;
            var result = new byte[] {
                (byte)(s >> 24), (byte)(s >> 16), (byte)(s >> 8), (byte)s
            };
            return Slice<byte>.Append(b, result);
        }

        public void Reset() { _hash = Offset32; }
        public long Size() => 4;
        public long BlockSize() => 1;
        public long Sum32() => (long)_hash;
    }

    internal class Fnv64Hash : IGoHash64
    {
        private const ulong Offset64 = 14695981039346656037;
        private const ulong Prime64 = 1099511628211;
        private readonly bool _isA;
        private ulong _hash;

        public Fnv64Hash(bool isA)
        {
            _isA = isA;
            _hash = Offset64;
        }

        public (int, string) Write(Slice<byte> p)
        {
            for (int i = 0; i < p.Len; i++)
            {
                if (_isA)
                {
                    _hash ^= p[i];
                    _hash *= Prime64;
                }
                else
                {
                    _hash *= Prime64;
                    _hash ^= p[i];
                }
            }
            return (p.Len, null!);
        }

        public Slice<byte> Sum(Slice<byte> b)
        {
            ulong s = _hash;
            var result = new byte[] {
                (byte)(s >> 56), (byte)(s >> 48), (byte)(s >> 40), (byte)(s >> 32),
                (byte)(s >> 24), (byte)(s >> 16), (byte)(s >> 8), (byte)s
            };
            return Slice<byte>.Append(b, result);
        }

        public void Reset() { _hash = Offset64; }
        public long Size() => 8;
        public long BlockSize() => 1;
        public long Sum64() => (long)_hash;
    }

    internal class Fnv128Hash : IGoHash
    {
        // FNV-128 offset basis and prime
        private readonly bool _isA;
        private ulong _hashHi;
        private ulong _hashLo;

        // FNV-128 offset basis: 144066263297769815596495629667062367629
        private const ulong OffsetHi128 = 0x6C62272E07BB0142UL;
        private const ulong OffsetLo128 = 0x62B821756295C58DUL;
        // FNV-128 prime: 309485009821345068724781371
        private const ulong PrimeHi128 = 0x0000000001000000UL;
        private const ulong PrimeLo128 = 0x000000000000013BUL;

        public Fnv128Hash(bool isA)
        {
            _isA = isA;
            _hashHi = OffsetHi128;
            _hashLo = OffsetLo128;
        }

        public (int, string) Write(Slice<byte> p)
        {
            for (int i = 0; i < p.Len; i++)
            {
                if (_isA)
                {
                    // XOR first
                    _hashLo ^= p[i];
                    // Multiply
                    Multiply128();
                }
                else
                {
                    // Multiply first
                    Multiply128();
                    // XOR
                    _hashLo ^= p[i];
                }
            }
            return (p.Len, null!);
        }

        private void Multiply128()
        {
            // Simple 128-bit multiply: (hi,lo) * (PrimeHi,PrimeLo)
            // Using the fact that FNV-128 prime is small enough
            ulong newLo = _hashLo * PrimeLo128;
            ulong carry = MulHi64(_hashLo, PrimeLo128);
            ulong newHi = _hashHi * PrimeLo128 + _hashLo * PrimeHi128 + carry;
            _hashLo = newLo;
            _hashHi = newHi;
        }

        private static ulong MulHi64(ulong a, ulong b)
        {
            // Upper 64 bits of 64x64 multiplication
            ulong aLo = a & 0xFFFFFFFF;
            ulong aHi = a >> 32;
            ulong bLo = b & 0xFFFFFFFF;
            ulong bHi = b >> 32;

            ulong cross1 = aHi * bLo;
            ulong cross2 = aLo * bHi;
            ulong lo = aLo * bLo;

            ulong mid = cross1 + (lo >> 32);
            mid += cross2;
            // If mid < cross2, we have a carry
            ulong carry = (mid < cross2) ? (1UL << 32) : 0;

            return aHi * bHi + (mid >> 32) + carry;
        }

        public Slice<byte> Sum(Slice<byte> b)
        {
            var result = new byte[] {
                (byte)(_hashHi >> 56), (byte)(_hashHi >> 48), (byte)(_hashHi >> 40), (byte)(_hashHi >> 32),
                (byte)(_hashHi >> 24), (byte)(_hashHi >> 16), (byte)(_hashHi >> 8), (byte)_hashHi,
                (byte)(_hashLo >> 56), (byte)(_hashLo >> 48), (byte)(_hashLo >> 40), (byte)(_hashLo >> 32),
                (byte)(_hashLo >> 24), (byte)(_hashLo >> 16), (byte)(_hashLo >> 8), (byte)_hashLo
            };
            return Slice<byte>.Append(b, result);
        }

        public void Reset()
        {
            _hashHi = OffsetHi128;
            _hashLo = OffsetLo128;
        }

        public long Size() => 16;
        public long BlockSize() => 1;
    }
}
